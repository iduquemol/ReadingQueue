using FluentValidation;
using ReadingQueue.Api.Requests;

namespace ReadingQueue.Api.Validators;

public sealed class MarkAsReadRequestValidator : AbstractValidator<MarkAsReadRequest>
{
    public MarkAsReadRequestValidator()
    {
        RuleFor(x => x.Notes)
            .MaximumLength(2000).When(x => x.Notes is not null);
    }
}
