using Microsoft.EntityFrameworkCore;
using SubTracker.Api.Common;
using SubTracker.Api.Data;
using SubTracker.Api.Dtos.Payments;
using SubTracker.Api.Mappings;

namespace SubTracker.Api.Services;

public class PaymentService(AppDbContext db, TimeProvider timeProvider) : IPaymentService
{
    private static readonly ServiceError SubscriptionNotFound =
        ServiceError.NotFound("Prenumerationen finns inte.");

    private static readonly ServiceError PaymentNotFound =
        ServiceError.NotFound("Betalningen finns inte.");


    //---------
    //-----Read
    //---------

    public async Task<ServiceResult<List<PaymentResponse>>> GetAllAsync(string userId, int subscriptionId)
    {
        if (!await db.Subscriptions.AnyAsync(s => s.Id == subscriptionId && s.UserId == userId))
        {
            return ServiceResult<List<PaymentResponse>>.Failure(SubscriptionNotFound);
        }

        var payments = await db.Payments
            .Where(p => p.SubscriptionId == subscriptionId)
            .OrderByDescending(p => p.PaidOn)
            .ToListAsync();

        return ServiceResult<List<PaymentResponse>>.Success(payments.Select(p => p.ToResponse()).ToList());
    }


    //-----------
    //-----Create
    //-----------

    public async Task<ServiceResult<PaymentResponse>> CreateAsync(string userId, int subscriptionId, PaymentRequest request)
    {
        var subscription = await db.Subscriptions
            .FirstOrDefaultAsync(s => s.Id == subscriptionId && s.UserId == userId);

        if (subscription is null)
        {
            return ServiceResult<PaymentResponse>.Failure(SubscriptionNotFound);
        }

        var payment = subscription.RegisterPayment(
            request.Amount ?? subscription.Price,
            request.PaidOn ?? timeProvider.GetToday());

        await db.SaveChangesAsync();

        return ServiceResult<PaymentResponse>.Success(payment.ToResponse());
    }


    //-----------
    //-----Delete
    //-----------

    public async Task<ServiceResult> DeleteAsync(string userId, int subscriptionId, int paymentId)
    {
        var payment = await db.Payments.FirstOrDefaultAsync(p =>
            p.Id == paymentId && p.SubscriptionId == subscriptionId && p.Subscription!.UserId == userId);

        if (payment is null)
        {
            return ServiceResult.Failure(PaymentNotFound);
        }

        db.Payments.Remove(payment);
        await db.SaveChangesAsync();

        return ServiceResult.Success();
    }
}
