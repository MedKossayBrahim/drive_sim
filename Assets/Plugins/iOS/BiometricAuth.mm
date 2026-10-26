#import <Foundation/Foundation.h>
#import <LocalAuthentication/LocalAuthentication.h>
#import <Security/Security.h>

// Face ID / Touch ID. Le visage reste dans le téléphone.
// Le jeton de session est rangé dans le trousseau et n'en sort qu'après une réussite.

static NSString *const kService = @"com.esprit.drivesim";
static NSString *const kAccount = @"refreshToken";

extern "C" void UnitySendMessage(const char *objectName, const char *methodName, const char *message);

static void SendToUnity(NSString *message)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        UnitySendMessage("AuthBridge", "OnNativeResult", message.UTF8String);
    });
}

static NSDictionary *BaseQuery(void)
{
    return @{
        (__bridge id)kSecClass: (__bridge id)kSecClassGenericPassword,
        (__bridge id)kSecAttrService: kService,
        (__bridge id)kSecAttrAccount: kAccount
    };
}

extern "C" bool Biometric_IsAvailable(void)
{
    LAContext *context = [[LAContext alloc] init];
    NSError *error = nil;
    return [context canEvaluatePolicy:LAPolicyDeviceOwnerAuthenticationWithBiometrics error:&error];
}

extern "C" bool Biometric_HasToken(void)
{
    NSMutableDictionary *query = [BaseQuery() mutableCopy];
    query[(__bridge id)kSecUseAuthenticationUI] = (__bridge id)kSecUseAuthenticationUIFail;
    OSStatus status = SecItemCopyMatching((__bridge CFDictionaryRef)query, NULL);
    return status == errSecSuccess
        || status == errSecInteractionNotAllowed
        || status == errSecAuthFailed;
}

extern "C" void Biometric_ClearToken(void)
{
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
    dispatch_async(dispatch_get_global_queue(QOS_CLASS_USER_INITIATED, 0), ^{
        CFErrorRef error = NULL;
        SecAccessControlRef access = SecAccessControlCreateWithFlags(
            kCFAllocatorDefault,
            kSecAttrAccessibleWhenPasscodeSetThisDeviceOnly,
            kSecAccessControlBiometryCurrentSet,
            &error);
        if (access == NULL)
        {
            SendToUnity(@"save:error:Activez un code et Face ID dans les reglages du telephone.");
            return;
        }

        SecItemDelete((__bridge CFDictionaryRef)BaseQuery());
        NSData *data = [value dataUsingEncoding:NSUTF8StringEncoding];
        NSDictionary *query = @{
            (__bridge id)kSecClass: (__bridge id)kSecClassGenericPassword,
            (__bridge id)kSecAttrService: kService,
            (__bridge id)kSecAttrAccount: kAccount,
            (__bridge id)kSecValueData: data,
            (__bridge id)kSecAttrAccessControl: (__bridge id)access
        };
        OSStatus status = SecItemAdd((__bridge CFDictionaryRef)query, NULL);
        CFRelease(access);
        if (status == errSecSuccess)
            SendToUnity(@"save:ok");
        else if (status == errSecUserCanceled)
            SendToUnity(@"save:cancel");
        else
            SendToUnity([NSString stringWithFormat:@"save:error:Trousseau %d", (int)status]);
    });
}

extern "C" void Biometric_LoadToken(const char *reason)
{
    NSString *why = reason != NULL ? [NSString stringWithUTF8String:reason] : @"Deverrouillez Drive Sim";
    dispatch_async(dispatch_get_global_queue(QOS_CLASS_USER_INITIATED, 0), ^{
        LAContext *context = [[LAContext alloc] init];
        context.localizedReason = why;
        NSDictionary *query = @{
            (__bridge id)kSecClass: (__bridge id)kSecClassGenericPassword,
            (__bridge id)kSecAttrService: kService,
            (__bridge id)kSecAttrAccount: kAccount,
            (__bridge id)kSecReturnData: @YES,
            (__bridge id)kSecMatchLimit: (__bridge id)kSecMatchLimitOne,
            (__bridge id)kSecUseAuthenticationContext: context
        };
        CFTypeRef result = NULL;
        OSStatus status = SecItemCopyMatching((__bridge CFDictionaryRef)query, &result);
        if (status == errSecSuccess && result != NULL)
        {
            NSData *data = (__bridge_transfer NSData *)result;
            NSString *token = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
            SendToUnity([@"load:ok:" stringByAppendingString:token ?: @""]);
            return;
        }
        if (result != NULL) CFRelease(result);
        if (status == errSecUserCanceled)
            SendToUnity(@"load:cancel");
        else if (status == errSecAuthFailed)
            SendToUnity(@"load:error:Visage non reconnu.");
        else if (status == errSecItemNotFound)
            SendToUnity(@"load:error:Aucune biométrie enregistrée sur cet appareil.");
        else
            SendToUnity([NSString stringWithFormat:@"load:error:Trousseau %d", (int)status]);
    });
}
