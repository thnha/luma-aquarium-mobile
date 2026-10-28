#import <Foundation/Foundation.h>
#import <objc/message.h>

// Firebase Unity 13 does not expose this iOS SDK API. Keep the callback rooted in managed code.
// FIRAuth is a Swift class since Firebase iOS 11, so FirebaseAuth.h only forward-declares it in
// Objective-C++; call it through the runtime instead of depending on FirebaseAuth-Swift.h.
extern "C" void LumaRevokeAppleToken(const char *code, void (*completion)(int))
{
    NSString *authorizationCode = code ? [NSString stringWithUTF8String:code] : nil;
    if (authorizationCode.length == 0) { completion(0); return; }

    Class authClass = NSClassFromString(@"FIRAuth");
    SEL authSel = NSSelectorFromString(@"auth");
    SEL revokeSel = NSSelectorFromString(@"revokeTokenWithAuthorizationCode:completion:");
    id auth = [authClass respondsToSelector:authSel] ? ((id (*)(Class, SEL))objc_msgSend)(authClass, authSel) : nil;
    if (![auth respondsToSelector:revokeSel]) { completion(0); return; }

    void (^done)(NSError *) = ^(NSError *error) {
        dispatch_async(dispatch_get_main_queue(), ^{ completion(error == nil ? 1 : 0); });
    };
    ((void (*)(id, SEL, NSString *, void (^)(NSError *)))objc_msgSend)(auth, revokeSel, authorizationCode, done);
}
