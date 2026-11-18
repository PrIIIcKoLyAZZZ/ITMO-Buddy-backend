using System.Runtime.ExceptionServices;
using ITMO.Buddy.Application.Abstractions.Persistence;
using ITMO.Buddy.Application.Contracts.Common;
using ITMO.Buddy.Domain.Common;

namespace ITMO.Buddy.Application.Errors;

public static class ApplicationErrorMapper
{
    public static ErrorResponse Map(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is OperationCanceledException)
        {
            ExceptionDispatchInfo.Throw(exception);
        }

        return exception switch
        {
            AppException error => error.Error,
            DomainException error => MapDomain(error),
            PersistenceException error => MapPersistence(error.Code),
            _ => InternalError()
        };
    }

    private static ErrorResponse MapDomain(DomainException error) =>
        error.Code switch
        {
            "listing.participants.inconsistent" =>
                new(error.Code, "The listing participant count could not be updated.", ErrorCategories.InternalError),
            "listing.response.not_found" =>
                new(error.Code, "Listing response was not found.", ErrorCategories.NotFound),
            "conversation.user.not_participant" or "message.read.by_sender" or "user.status.not_active" =>
                new(error.Code, "This operation is not permitted.", ErrorCategories.Forbidden),
            "listing.closed" or "listing.participants.below_accepted" or
                "listing.participants.full" or "listing.response.duplicate" or
                "listing.response.window_closed" or "listing_response.already_decided" or
                "user.status.invalid_transition" =>
                new(error.Code, "The current state does not allow this operation.", ErrorCategories.Conflict),
            "user.email.invalid" or "entity.id.empty" or
                "listing.title.invalid" or "listing.description.invalid" or
                "listing.category.invalid" or "listing.format.invalid" or
                "listing.participants.invalid" or "listing.deadline.invalid" or
                "listing.event_date.invalid" or "listing.dates.invalid_order" or
                "listing.skill.invalid" or "listing.skill.duplicate" or "listing.skill_id.empty" or
                "listing.skills.null" or "listing.response.self_response" or
                "listing_response.message.too_long" or
                "profile.about_me.too_long" or "profile.course.invalid" or "profile.faculty.invalid" or
                "profile.goal.duplicate" or "profile.goal.invalid" or "profile.goals.null" or
                "profile.interest.duplicate" or "profile.interest_id.empty" or "profile.interests.limit_exceeded" or
                "profile.skill.duplicate" or "profile.skill_id.empty" or "profile.skills.limit_exceeded" or
                "reference.name.empty" or "message.text.empty" or "message.text.too_long" or
                "user_like.self_like" or "user_block.self_block" =>
                new(error.Code, "The request violates a business rule.", ErrorCategories.Validation),
            _ => InternalError()
        };

    private static ErrorResponse MapPersistence(PersistenceErrorCode code) =>
        code switch
        {
            PersistenceErrorCode.UniqueConstraintViolation =>
                new("persistence.duplicate", "A record with the same key already exists.", ErrorCategories.Conflict),
            PersistenceErrorCode.ForeignKeyViolation =>
                new("persistence.reference_conflict", "A related record prevents this change.", ErrorCategories.Conflict),
            PersistenceErrorCode.CheckConstraintViolation =>
                new("persistence.constraint_violation", "The requested change violates a data rule.", ErrorCategories.Validation),
            PersistenceErrorCode.NotNullViolation =>
                new("persistence.required_value_missing", "A required value could not be saved.", ErrorCategories.InternalError),
            PersistenceErrorCode.ConcurrencyConflict =>
                new("persistence.concurrent_change", "The record was changed by another operation.", ErrorCategories.Conflict),
            PersistenceErrorCode.SerializationFailure =>
                new("persistence.serialization_conflict", "The operation conflicted with a concurrent change.", ErrorCategories.Conflict),
            PersistenceErrorCode.Deadlock =>
                new("persistence.deadlock", "A concurrent operation prevented this change.", ErrorCategories.Conflict),
            PersistenceErrorCode.ConnectionFailure =>
                new("persistence.unavailable", "Storage is temporarily unavailable.", ErrorCategories.InternalError),
            PersistenceErrorCode.TransactionFailure =>
                new("persistence.transaction_failed", "The transaction could not be completed.", ErrorCategories.InternalError),
            PersistenceErrorCode.TransactionOutcomeUnknown =>
                new("persistence.transaction_outcome_unknown", "The transaction outcome could not be confirmed.", ErrorCategories.InternalError),
            PersistenceErrorCode.StorageFailure =>
                new("persistence.storage_failure", "The storage operation failed.", ErrorCategories.InternalError),
            _ => InternalError()
        };

    private static ErrorResponse InternalError() =>
        new("application.internal_error", "An unexpected error occurred.", ErrorCategories.InternalError);
}
