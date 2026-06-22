namespace SalesCom.Application.UnitTests.Behaviors;

using FluentValidation;
using SalesCom.Application.Behaviours;
using SalesCom.Application.Messaging;
using SalesCom.Domain.Common;

public sealed class ValidationDecoratorTests
{
    private sealed record FakeCommand(string Name) : ICommand<Result<string>>;

    private sealed class FakeValidator : AbstractValidator<FakeCommand>
    {
        public FakeValidator() => RuleFor(c => c.Name).NotEmpty();
    }

    private sealed class FakeHandler : ICommandHandler<FakeCommand, Result<string>>
    {
        public int Calls { get; private set; }

        public Task<Result<string>> HandleAsync(FakeCommand command, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Result.Success(command.Name.ToUpperInvariant()));
        }
    }

    [Fact]
    public async Task ValidCommand_InvokesInnerHandler()
    {
        var inner = new FakeHandler();
        var decorator = new ValidationCommandHandlerDecorator<FakeCommand, Result<string>>(inner, [new FakeValidator()]);

        var result = await decorator.HandleAsync(new FakeCommand("ok"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("OK");
        inner.Calls.Should().Be(1);
    }

    [Fact]
    public async Task InvalidCommand_ShortCircuitsWithValidationFailure_AndNeverCallsInner()
    {
        var inner = new FakeHandler();
        var decorator = new ValidationCommandHandlerDecorator<FakeCommand, Result<string>>(inner, [new FakeValidator()]);

        var result = await decorator.HandleAsync(new FakeCommand(string.Empty), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
        inner.Calls.Should().Be(0);
    }
}
