package com.drivesim.auth;

import android.app.Activity;
import android.content.SharedPreferences;
import android.hardware.biometrics.BiometricManager;
import android.hardware.biometrics.BiometricPrompt;
import android.os.Build;
import android.os.CancellationSignal;
import android.security.keystore.KeyGenParameterSpec;
import android.security.keystore.KeyPermanentlyInvalidatedException;
import android.security.keystore.KeyProperties;
import android.util.Base64;
import android.util.Log;

import java.nio.charset.StandardCharsets;
import java.security.KeyStore;

import javax.crypto.Cipher;
import javax.crypto.KeyGenerator;
import javax.crypto.SecretKey;
import javax.crypto.spec.GCMParameterSpec;

// Fenêtre biométrique Android. Le visage ou l'empreinte déverrouille un jeton
// gardé dans le coffre du téléphone. Rien de biométrique n'est envoyé au serveur.
public final class BiometricPlugin {
    private static final String TAG = "DriveSimAuth";
    private static final String PREFS = "drive_sim_auth";
    private static final String KEY_NAME = "drive_sim_biometric_key";
    private static final String CIPHER_PREF = "refresh_cipher";
    private static final String IV_PREF = "refresh_iv";
    private static final int GCM_TAG_BITS = 128;

    private static ResultCallback active;
    private static boolean busy;
    private static boolean replied;
    private static boolean encrypting;
    private static byte[] pendingPayload;

    private BiometricPlugin() {}

    public static boolean isAvailable(Activity activity) {
        if (activity == null || Build.VERSION.SDK_INT < Build.VERSION_CODES.P) return false;
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.Q) return true;
        BiometricManager manager = BiometricManager.from(activity);
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
            return Api30.canAuthenticate(manager);
        }
        return manager.canAuthenticate() == BiometricManager.BIOMETRIC_SUCCESS;
    }

    public static boolean hasToken(Activity activity) {
        if (activity == null) return false;
        SharedPreferences prefs = prefs(activity);
        return prefs.contains(CIPHER_PREF) && prefs.contains(IV_PREF);
    }

    public static void clearToken(Activity activity) {
        if (activity == null) return;
        prefs(activity).edit().clear().apply();
        try {
            KeyStore store = KeyStore.getInstance("AndroidKeyStore");
            store.load(null);
            store.deleteEntry(KEY_NAME);
        } catch (Exception exception) {
            Log.w(TAG, "Suppression de la cle impossible", exception);
        }
    }

    public static void saveToken(Activity activity, String token, ResultCallback callback) {
        if (!begin(callback, true)) return;
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.P) {
            send("save:error:Android 9 ou plus recent est requis pour la biometrie.");
            return;
        }
        if (activity == null || token == null || token.isEmpty()) {
            send("save:error:Jeton vide.");
            return;
        }
        pendingPayload = token.getBytes(StandardCharsets.UTF_8);
        try {
            Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
            cipher.init(Cipher.ENCRYPT_MODE, getOrCreateKey());
            prompt(activity, cipher);
        } catch (KeyPermanentlyInvalidatedException exception) {
            clearToken(activity);
            send("save:error:La biometrie de l'appareil a change. Reessayez.");
        } catch (Exception exception) {
            busy = false;
            send("save:error:" + clean(exception.getMessage()));
        }
    }

    public static void loadToken(Activity activity, ResultCallback callback) {
        if (!begin(callback, false)) return;
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.P) {
            send("load:error:Android 9 ou plus recent est requis pour la biometrie.");
            return;
        }
        if (activity == null || !hasToken(activity)) {
            send("load:error:Aucune biometrie enregistree sur cet appareil.");
            return;
        }
        try {
            SharedPreferences prefs = prefs(activity);
            byte[] iv = Base64.decode(prefs.getString(IV_PREF, ""), Base64.NO_WRAP);
            pendingPayload = Base64.decode(prefs.getString(CIPHER_PREF, ""), Base64.NO_WRAP);
            Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
            cipher.init(Cipher.DECRYPT_MODE, getOrCreateKey(), new GCMParameterSpec(GCM_TAG_BITS, iv));
            prompt(activity, cipher);
        } catch (KeyPermanentlyInvalidatedException exception) {
            clearToken(activity);
            send("load:error:La biometrie de l'appareil a change. Utilisez le mot de passe.");
        } catch (Exception exception) {
            busy = false;
            send("load:error:" + clean(exception.getMessage()));
        }
    }

    private static void prompt(Activity activity, Cipher cipher) {
        if (busy) {
            send(encrypting ? "save:error:Une verification est deja en cours." : "load:error:Une verification est deja en cours.");
            return;
        }
        busy = true;
        BiometricPrompt.Builder builder = new BiometricPrompt.Builder(activity)
                .setTitle("Drive Sim")
                .setSubtitle(encrypting ? "Activer la connexion biometrique" : "Connexion")
                .setDescription("Confirmez votre identite. Le visage reste sur le telephone.")
                .setConfirmationRequired(false);
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
            Api30.allowWeakBiometric(builder);
        }
        builder.setNegativeButton("Mot de passe", activity.getMainExecutor(), (dialog, which) ->
                send(encrypting ? "save:cancel" : "load:cancel"));
        BiometricPrompt prompt = builder.build();
        prompt.authenticate(
                new BiometricPrompt.CryptoObject(cipher),
                new CancellationSignal(),
                activity.getMainExecutor(),
                new BiometricPrompt.AuthenticationCallback() {
                    @Override
                    public void onAuthenticationError(int errorCode, CharSequence errString) {
                        busy = false;
                        if (errorCode == BiometricPrompt.BIOMETRIC_ERROR_USER_CANCELED
                                || errorCode == BiometricPrompt.BIOMETRIC_ERROR_CANCELED
                                || errorCode == BiometricPrompt.BIOMETRIC_ERROR_NEGATIVE_BUTTON) {
                            send(encrypting ? "save:cancel" : "load:cancel");
                            return;
                        }
                        send((encrypting ? "save:error:" : "load:error:") + clean(String.valueOf(errString)));
                    }

                    @Override
                    public void onAuthenticationSucceeded(BiometricPrompt.AuthenticationResult result) {
                        busy = false;
                        try {
                            Cipher unlocked = result.getCryptoObject().getCipher();
                            if (encrypting) {
                                byte[] encrypted = unlocked.doFinal(pendingPayload);
                                writeCipher(activity, encrypted, unlocked.getIV());
                                send("save:ok");
                            } else {
                                byte[] plain = unlocked.doFinal(pendingPayload);
                                send("load:ok:" + new String(plain, StandardCharsets.UTF_8));
                            }
                        } catch (KeyPermanentlyInvalidatedException exception) {
                            clearToken(activity);
                            send((encrypting ? "save:error:" : "load:error:") + "La biometrie de l'appareil a change. Utilisez le mot de passe.");
                        } catch (Exception exception) {
                            send((encrypting ? "save:error:" : "load:error:") + clean(exception.getMessage()));
                        }
                    }
                });
    }

    private static SecretKey getOrCreateKey() throws Exception {
        KeyStore store = KeyStore.getInstance("AndroidKeyStore");
        store.load(null);
        if (store.containsAlias(KEY_NAME)) {
            return (SecretKey) store.getKey(KEY_NAME, null);
        }
        KeyGenerator generator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore");
        KeyGenParameterSpec.Builder builder = new KeyGenParameterSpec.Builder(
                KEY_NAME,
                KeyProperties.PURPOSE_ENCRYPT | KeyProperties.PURPOSE_DECRYPT)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                .setKeySize(256)
                .setInvalidatedByBiometricEnrollment(true);
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
            Api30.allowWeakKey(builder);
        } else {
            builder.setUserAuthenticationRequired(true);
            builder.setUserAuthenticationValidityDurationSeconds(-1);
        }
        generator.init(builder.build());
        return generator.generateKey();
    }

    private static void writeCipher(Activity activity, byte[] encrypted, byte[] iv) {
        prefs(activity).edit()
                .putString(CIPHER_PREF, Base64.encodeToString(encrypted, Base64.NO_WRAP))
                .putString(IV_PREF, Base64.encodeToString(iv, Base64.NO_WRAP))
                .apply();
    }

    private static SharedPreferences prefs(Activity activity) {
        return activity.getSharedPreferences(PREFS, Activity.MODE_PRIVATE);
    }

    private static boolean begin(ResultCallback callback, boolean willEncrypt) {
        if (busy) return false;
        active = callback;
        encrypting = willEncrypt;
        replied = false;
        return true;
    }

    private static void send(String message) {
        if (replied) return;
        replied = true;
        busy = false;
        Log.d(TAG, message);
        ResultCallback callback = active;
        if (callback != null) callback.onResult(message);
    }

    private static String clean(String message) {
        if (message == null || message.trim().isEmpty()) return "Biometrie indisponible.";
        return message.replace('\n', ' ').replace('\r', ' ');
    }

    // Classe chargée seulement à partir d'Android 11, là où ces constantes existent.
    static final class Api30 {
        private Api30() {}

        static boolean canAuthenticate(BiometricManager manager) {
            return manager.canAuthenticate(BiometricManager.Authenticators.BIOMETRIC_WEAK)
                    == BiometricManager.BIOMETRIC_SUCCESS;
        }

        static void allowWeakBiometric(BiometricPrompt.Builder builder) {
            builder.setAllowedAuthenticators(BiometricManager.Authenticators.BIOMETRIC_WEAK);
        }

        static void allowWeakKey(KeyGenParameterSpec.Builder builder) {
            builder.setUserAuthenticationParameters(
                    0,
                    KeyProperties.AUTH_BIOMETRIC_STRONG | KeyProperties.AUTH_BIOMETRIC_WEAK);
        }
    }
}
