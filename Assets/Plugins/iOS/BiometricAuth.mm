#import <Foundation/Foundation.h>
#import <LocalAuthentication/LocalAuthentication.h>
#import <Security/Security.h>
#import <UIKit/UIKit.h>

// Face ID. Le visage reste dans le téléphone.
// SecItemAdd n'affiche pas la fenêtre Face ID : on la demande nous-mêmes,
// puis le jeton est rangé dans le trousseau.

static NSString *const kService = @"com.esprit.drivesim";
static NSString *const kAccount = @"refreshToken";
static NSString *const kFlag = @"drivesim.biometric.enabled";

extern "C" void UnitySendMessage(const char *objectName, const char *methodName, const char *message);

typedef void (^AuthDone)(LAContext *context, NSString *problem);

static void SendToUnity(NSString *message)
{
    if (message == nil)
        return;
    dispatch_async(dispatch_get_main_queue(), ^{
        UnitySendMessage("AuthBridge", "OnNativeResult", message.UTF8String);
    });
}

static void SetEnabled(BOOL enabled)
{
    [[NSUserDefaults standardUserDefaults] setBool:enabled forKey:kFlag];
}

static NSDictionary *BaseQuery(void)
{
    return @{
        (__bridge id)kSecClass: (__bridge id)kSecClassGenericPassword,
        (__bridge id)kSecAttrService: kService,
        (__bridge id)kSecAttrAccount: kAccount
    };
}

static NSString *MessageForFace(NSError *error)
{
    if (error == nil)
        return @"Face ID indisponible.";
    switch (error.code)
    {
        case LAErrorBiometryNotEnrolled:
            return @"Aucun visage enregistré. Ajoutez Face ID dans Réglages > Face ID et code.";
        case LAErrorBiometryNotAvailable:
            return @"Face ID n'est pas autorisé. Ouvrez Réglages > Face ID et code, puis MultiRide.";
        case LAErrorBiometryLockout:
            return @"Face ID est bloqué. Déverrouillez l'iPhone avec le code, puis réessayez.";
        case LAErrorPasscodeNotSet:
            return @"Activez un code et Face ID dans les réglages du téléphone.";
        case LAErrorAuthenticationFailed:
            return @"Visage non reconnu.";
        default:
            break;
    }
    if (error.localizedDescription.length > 0)
        return error.localizedDescription;
    return @"Face ID a échoué.";
}

static BOOL IsCancel(NSError *error)
{
    if (error == nil)
        return NO;
    switch (error.code)
    {
        case LAErrorUserCancel:
        case LAErrorSystemCancel:
        case LAErrorAppCancel:
        case LAErrorUserFallback:
            return YES;
        default:
            return NO;
    }
}

static void AskFaceNow(NSString *reason, AuthDone done)
{
    LAContext *context = [[LAContext alloc] init];
    context.localizedCancelTitle = @"Annuler";
    NSError *canError = nil;
    if (![context canEvaluatePolicy:LAPolicyDeviceOwnerAuthenticationWithBiometrics error:&canError])
    {
        done(nil, MessageForFace(canError));
        return;
    }

    [context evaluatePolicy:LAPolicyDeviceOwnerAuthenticationWithBiometrics
            localizedReason:reason
                      reply:^(BOOL success, NSError *error) {
        if (success)
        {
            done(context, nil);
            return;
        }
        if (IsCancel(error))
            done(nil, @"cancel");
        else
            done(nil, MessageForFace(error));
    }];
}

static void AskFace(NSString *reason, int tries, AuthDone done)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        UIApplicationState state = [UIApplication sharedApplication].applicationState;
        if (state != UIApplicationStateActive && tries < 20)
        {
            dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.2 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
                AskFace(reason, tries + 1, done);
            });
            return;
        }
        AskFaceNow(reason, done);
    });
}

static void RemoveExisting(LAContext *context)
{
    if (context != nil)
    {
        NSMutableDictionary *query = [BaseQuery() mutableCopy];
        query[(__bridge id)kSecUseAuthenticationContext] = context;
        SecItemDelete((__bridge CFDictionaryRef)query);
    }
    SecItemDelete((__bridge CFDictionaryRef)BaseQuery());
}

static NSString *StoreToken(LAContext *context, NSString *value)
{
    RemoveExisting(context);
    NSData *data = [value dataUsingEncoding:NSUTF8StringEncoding];
    NSDictionary *query = @{
        (__bridge id)kSecClass: (__bridge id)kSecClassGenericPassword,
        (__bridge id)kSecAttrService: kService,
        (__bridge id)kSecAttrAccount: kAccount,
        (__bridge id)kSecValueData: data,
        (__bridge id)kSecAttrAccessible: (__bridge id)kSecAttrAccessibleWhenUnlockedThisDeviceOnly
    };
    OSStatus status = SecItemAdd((__bridge CFDictionaryRef)query, NULL);
    if (status == errSecSuccess)
        return nil;
    if (status == errSecDuplicateItem)
    {
        NSDictionary *update = @{ (__bridge id)kSecValueData: data };
        status = SecItemUpdate((__bridge CFDictionaryRef)BaseQuery(), (__bridge CFDictionaryRef)update);
        if (status == errSecSuccess)
            return nil;
    }
    return [NSString stringWithFormat:@"Trousseau %d. Réessayez après avoir accepté Face ID.", (int)status];
}

static NSString *ReadToken(LAContext *context)
{
    NSMutableDictionary *query = [BaseQuery() mutableCopy];
    query[(__bridge id)kSecReturnData] = @YES;
    query[(__bridge id)kSecMatchLimit] = (__bridge id)kSecMatchLimitOne;
    if (context != nil)
        query[(__bridge id)kSecUseAuthenticationContext] = context;
    CFTypeRef result = NULL;
    OSStatus status = SecItemCopyMatching((__bridge CFDictionaryRef)query, &result);
    if (status == errSecSuccess && result != NULL)
    {
        NSData *data = (__bridge_transfer NSData *)result;
        return [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding] ?: @"";
    }
    if (result != NULL)
        CFRelease(result);
    if (status == errSecItemNotFound)
        return nil;
    return @"";
}

extern "C" bool Biometric_IsAvailable(void)
{
    LAContext *context = [[LAContext alloc] init];
    NSError *error = nil;
    return [context canEvaluatePolicy:LAPolicyDeviceOwnerAuthenticationWithBiometrics error:&error];
}

extern "C" bool Biometric_HasToken(void)
{
    if ([[NSUserDefaults standardUserDefaults] boolForKey:kFlag])
        return true;

    LAContext *context = [[LAContext alloc] init];
    context.interactionNotAllowed = YES;
    NSMutableDictionary *query = [BaseQuery() mutableCopy];
    query[(__bridge id)kSecUseAuthenticationContext] = context;
    OSStatus status = SecItemCopyMatching((__bridge CFDictionaryRef)query, NULL);
    return status == errSecSuccess
        || status == errSecInteractionNotAllowed
        || status == errSecAuthFailed;
}

extern "C" void Biometric_ClearToken(void)
{
    SetEnabled(NO);
    SecItemDelete((__bridge CFDictionaryRef)BaseQuery());
}

extern "C" void Biometric_SaveToken(const char *token)
{
    if (token == NULL || token[0] == '\0')
    {
        SendToUnity(@"save:error:Jeton vide.");
        return;
    }

    NSString *value = [NSString stringWithUTF8String:token];
    AskFace(@"Confirmez votre visage pour activer MultiRide", 0, ^(LAContext *context, NSString *problem) {
        if (problem != nil)
        {
            if ([problem isEqualToString:@"cancel"])
                SendToUnity(@"save:cancel");
            else
                SendToUnity([@"save:error:" stringByAppendingString:problem]);
            return;
        }

        NSString *error = StoreToken(context, value);
        if (error != nil)
        {
            SendToUnity([@"save:error:" stringByAppendingString:error]);
            return;
        }
        SetEnabled(YES);
        SendToUnity(@"save:ok");
    });
}

extern "C" void Biometric_LoadToken(const char *reason)
{
    NSString *why = reason != NULL && reason[0] != '\0'
        ? [NSString stringWithUTF8String:reason]
        : @"Déverrouillez MultiRide avec Face ID";
    AskFace(why, 0, ^(LAContext *context, NSString *problem) {
        if (problem != nil)
        {
            if ([problem isEqualToString:@"cancel"])
                SendToUnity(@"load:cancel");
            else
                SendToUnity([@"load:error:" stringByAppendingString:problem]);
            return;
        }

        NSString *token = ReadToken(context);
        if (token == nil)
        {
            SetEnabled(NO);
            SendToUnity(@"load:error:Aucune biométrie enregistrée. Connectez-vous avec le mot de passe.");
            return;
        }
        if (token.length == 0)
        {
            SendToUnity(@"load:error:Visage reconnu, mais le compte local est illisible. Utilisez le mot de passe.");
            return;
        }
        SetEnabled(YES);
        SendToUnity([@"load:ok:" stringByAppendingString:token]);
    });
}
