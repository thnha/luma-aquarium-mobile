# Delete account

Approved in chat on 2026-10-28: use the interactive cream/green Settings preview, a red Delete account row visible only for signed-in players, and a confirmation listing tank/fish/decor, coins/purchased items, and device/cloud data. Deletion is permanent. Keep/cancel does nothing. Show progress, retry on failure, and success before returning to StartGame.

Delete the calling player's protected Cloud Save and Firebase/Unity Authentication identities. Clear device progress, backups and guest/session markers; preserve language and user-created photos. A durable checkpoint lets interrupted deletion resume without recreating the removed identity or importing old progress. Block gameplay and shop writes while deletion is pending. Reauthenticate the original social identity before destructive work; canceling native sign-in does not delete anything. Revoke Apple's token on iOS.

Approaches: unlink/sign-out would retain the account and progress and fails the request; client-only deletion cannot remove protected Cloud Save. Use a caller-scoped Cloud Code deletion endpoint plus the existing SDKs and uGUI builders. No new dependencies, environment changes, commits or deployment. Deploy the updated Cloud Code before releasing the client.
