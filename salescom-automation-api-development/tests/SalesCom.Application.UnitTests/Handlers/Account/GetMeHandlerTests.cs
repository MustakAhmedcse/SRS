namespace SalesCom.Application.UnitTests.Handlers.Account;

using System.Linq.Expressions;
using SalesCom.Application.Interfaces;
using SalesCom.Application.Queries.Account.GetMe;
using SalesCom.Domain.Entities.Identity;
using SalesCom.Domain.Errors;
using SalesCom.Domain.Interfaces;

public sealed class GetMeHandlerTests
{
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly IUserRightsQuery _rightsQuery = Substitute.For<IUserRightsQuery>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IGenericRepository<User> _users = Substitute.For<IGenericRepository<User>>();

    public GetMeHandlerTests()
    {
        _unitOfWork.Repository<User>().Returns(_users);
    }

    private GetMeHandler CreateSut() => new(_currentUser, _rightsQuery, _unitOfWork);

    [Fact]
    public async Task Returns_NotFound_when_caller_is_unauthenticated()
    {
        _currentUser.IsAuthenticated.Returns(false);

        var result = await CreateSut().HandleAsync(new GetMeQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotFound);
    }

    [Fact]
    public async Task Returns_NotFound_when_user_row_is_missing()
    {
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns("102941");
        _users.FirstOrDefaultAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var result = await CreateSut().HandleAsync(new GetMeQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.NotFound);
    }

    [Fact]
    public async Task Returns_profile_from_db_and_rights_from_db()
    {
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns("102941");
        _rightsQuery.GetRightsAsync("102941", Arg.Any<CancellationToken>()).Returns([1001, 1002]);

        var user = new User
        {
            Id = 7,
            UserId = "102941",
            UserName = "alice",
            FullName = "Alice Rahman",
            Email = "alice@example.com",
            MobileNo = "01900000000",
            Department = "CCD",
        };
        _users.FirstOrDefaultAsync(Arg.Any<Expression<Func<User, bool>>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(user);

        var result = await CreateSut().HandleAsync(new GetMeQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be("102941");
        result.Value.UserName.Should().Be("alice");
        result.Value.FullName.Should().Be("Alice Rahman");
        result.Value.Email.Should().Be("alice@example.com");
        result.Value.MobileNo.Should().Be("01900000000");
        result.Value.Department.Should().Be("CCD");
        result.Value.Rights.Should().Equal(1001, 1002);
    }
}
