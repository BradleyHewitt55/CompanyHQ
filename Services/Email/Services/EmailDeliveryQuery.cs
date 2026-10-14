using Microsoft.Extensions.DependencyInjection;
using CompanyHQ.Email.Contracts.Models;
using CompanyHQ.Email.Contracts.Ports;
using CompanyHQ.Persistence.Services;

namespace CompanyHQ.Email.Services;

internal sealed class EmailDeliveryQuery(IServiceScopeFactory scopeFactory) : IEmailDeliveryQuery
{
    public async Task<EmailDeliveryState?> GetAsync(string idempotencyKey,CancellationToken cancellationToken=default)
    {
        await using var scope=scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IRepository<QueuedEmail>>().Query()
            .Where(x=>x.IdempotencyKey==idempotencyKey)
            .Select(x=>new EmailDeliveryState(x.Id,x.Status,x.ProviderMessageId))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
