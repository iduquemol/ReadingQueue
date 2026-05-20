using FluentValidation;
using ReadingQueue.Api.Requests;

namespace ReadingQueue.Api.Validators;

public sealed class UpdateBookRequestValidator : AbstractValidator<UpdateBookRequest>
{
    public UpdateBookRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("El título es obligatorio.")
            .MaximumLength(500);

        RuleFor(x => x.Author)
            .NotEmpty().WithMessage("El autor es obligatorio.")
            .MaximumLength(300);

        RuleFor(x => x.Genre)
            .NotEmpty().WithMessage("El género es obligatorio.")
            .MaximumLength(100);

        RuleFor(x => x.Country)
            .NotEmpty().WithMessage("El país es obligatorio.")
            .MaximumLength(100);

        RuleFor(x => x.Priority)
            .InclusiveBetween(1, 5)
            .WithMessage("La prioridad debe estar entre 1 y 5.");

        RuleFor(x => x.MentalEnergy)
            .NotEmpty().WithMessage("El nivel de energía mental es obligatorio.")
            .MaximumLength(100);

        RuleFor(x => x.RecommendedMood)
            .NotEmpty().WithMessage("El ánimo recomendado es obligatorio.")
            .MaximumLength(200);

        RuleFor(x => x.RotationCategory)
            .NotEmpty().WithMessage("La categoría de rotación es obligatoria.")
            .MaximumLength(100);

        RuleFor(x => x.WhyRead)
            .MaximumLength(1000).When(x => x.WhyRead is not null);

        RuleFor(x => x.Notes)
            .MaximumLength(2000).When(x => x.Notes is not null);
    }
}
