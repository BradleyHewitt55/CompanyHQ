using Microsoft.Extensions.DependencyInjection;
using CompanyHQ.Services.Email.Contracts.Models;
using CompanyHQ.Services.Email.Contracts.Ports;
using CompanyHQ.Services.Persistence.Services;

namespace CompanyHQ.Services.Email.Services;

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
