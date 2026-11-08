using MegaCrit.Sts2.Core.Models;
using ShoujoKageki.Core.V1;
namespace ShoujoKagekiJunna.Combat;
internal static class DamageSourceScopes
{
    private static IDisposable? _subscription;
    internal static void Install() => _subscription ??= CoreApi.Get<IModelExecutionScopes>().Register((model, play) =>
        model is CardModel card && play != null ? DamageExecution.BeginCardPlay(card, play) : DamageExecution.UseDamageSource(model));
}
