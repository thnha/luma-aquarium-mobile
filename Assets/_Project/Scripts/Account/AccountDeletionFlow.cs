using System;
using System.Threading.Tasks;

namespace Lumaquarium.Account
{
    public enum AccountDeletionStage { None, Confirmed, CloudDeleted, IdentityDeletionStarted, IdentityDeleted, AccountDeletionStarted, AccountDeleted }

    // Durable checkpoints are written only after a step succeeds. Retrying never signs up a replacement identity.
    public static class AccountDeletionFlow
    {
        public static async Task Run(AccountDeletionStage stage, Func<Task> prepare, Func<Task> deleteCloud,
            Func<Task> deleteIdentity, Func<Task> deleteAccount, Action<AccountDeletionStage> checkpoint, Action clearLocal)
        {
            if (stage < AccountDeletionStage.IdentityDeletionStarted) await prepare();
            if (stage == AccountDeletionStage.None) checkpoint(AccountDeletionStage.Confirmed);
            if (stage < AccountDeletionStage.CloudDeleted)
            {
                await deleteCloud();
                checkpoint(AccountDeletionStage.CloudDeleted);
            }
            if (stage < AccountDeletionStage.IdentityDeleted)
            {
                if (stage < AccountDeletionStage.IdentityDeletionStarted) checkpoint(AccountDeletionStage.IdentityDeletionStarted);
                await deleteIdentity();
                checkpoint(AccountDeletionStage.IdentityDeleted);
            }
            if (stage < AccountDeletionStage.AccountDeleted)
            {
                if (stage < AccountDeletionStage.AccountDeletionStarted) checkpoint(AccountDeletionStage.AccountDeletionStarted);
                await deleteAccount();
                checkpoint(AccountDeletionStage.AccountDeleted);
            }
            clearLocal();
        }
    }
}
