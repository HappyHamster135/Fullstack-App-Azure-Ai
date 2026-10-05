using Microsoft.EntityFrameworkCore;
using SubTracker.Api.Common;
using SubTracker.Api.Data;
using SubTracker.Api.Dtos.Subscriptions;
using SubTracker.Api.Entities;
using SubTracker.Api.Mappings;

namespace SubTracker.Api.Services;

public class SubscriptionService(AppDbContext db) : ISubscriptionService
{
    // Registreringen är öppen och prognosens arbete växer med antalet prenumerationer,
    // så antalet per användare är begränsat. En vanlig användare har långt färre.
    public const int MaxPerUser = 200;

    private static readonly ServiceError NotFound =
        ServiceError.NotFound("Prenumerationen finns inte.");

    private static readonly ServiceError CategoryNotFound = ServiceError.Validation(
        new Dictionary<string, string[]> { [nameof(SubscriptionRequest.CategoryId)] = ["Kategorin finns inte."] });

    private static readonly ServiceError LimitReached =
        ServiceError.Conflict($"Du kan ha högst {MaxPerUser} prenumerationer.");


    //---------
    //-----Read
    //---------

    public async Task<List<SubscriptionResponse>> GetAllAsync(string userId)
    {
        var subscriptions = await db.Subscriptions
            .Include(s => s.Category)
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.NextPaymentDate)
            .ToListAsync();

        return subscriptions.Select(s => s.ToResponse()).ToList();
    }

    public async Task<ServiceResult<SubscriptionResponse>> GetByIdAsync(string userId, int id)
    {
        var subscription = await FindOwnedAsync(userId, id);

        return subscription is null
            ? ServiceResult<SubscriptionResponse>.Failure(NotFound)
            : ServiceResult<SubscriptionResponse>.Success(subscription.ToResponse());
    }


    //-----------
    //-----Create
    //-----------

    public async Task<ServiceResult<SubscriptionResponse>> CreateAsync(string userId, SubscriptionRequest request)
    {
        var category = await FindOwnedCategoryAsync(userId, request.CategoryId);

        if (category is null)
        {
            return ServiceResult<SubscriptionResponse>.Failure(CategoryNotFound);
        }

        if (await db.Subscriptions.CountAsync(s => s.UserId == userId) >= MaxPerUser)
        {
            return ServiceResult<SubscriptionResponse>.Failure(LimitReached);
        }

        var subscription = new Subscription { UserId = userId, CreatedAt = DateTime.UtcNow };
        request.ApplyTo(subscription, category);

        db.Subscriptions.Add(subscription);
        await db.SaveChangesAsync();

        return ServiceResult<SubscriptionResponse>.Success(subscription.ToResponse());
    }


    //-----------
    //-----Update
    //-----------

    public async Task<ServiceResult<SubscriptionResponse>> UpdateAsync(string userId, int id, SubscriptionRequest request)
    {
        var subscription = await FindOwnedAsync(userId, id);

        if (subscription is null)
        {
            return ServiceResult<SubscriptionResponse>.Failure(NotFound);
        }

        var category = await FindOwnedCategoryAsync(userId, request.CategoryId);

        if (category is null)
        {
            return ServiceResult<SubscriptionResponse>.Failure(CategoryNotFound);
        }

        request.ApplyTo(subscription, category);
        await db.SaveChangesAsync();

        return ServiceResult<SubscriptionResponse>.Success(subscription.ToResponse());
    }


    //-----------
    //-----Delete
    //-----------

    public async Task<ServiceResult> DeleteAsync(string userId, int id)
    {
        var subscription = await FindOwnedAsync(userId, id);

        if (subscription is null)
        {
            return ServiceResult.Failure(NotFound);
        }

        db.Subscriptions.Remove(subscription);
        await db.SaveChangesAsync();

        return ServiceResult.Success();
    }


    //------------
    //-----Helpers
    //------------

    private Task<Subscription?> FindOwnedAsync(string userId, int id) =>
        db.Subscriptions
            .Include(s => s.Category)
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId);

    private Task<Category?> FindOwnedCategoryAsync(string userId, int? categoryId) =>
        db.Categories.FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId);
}
