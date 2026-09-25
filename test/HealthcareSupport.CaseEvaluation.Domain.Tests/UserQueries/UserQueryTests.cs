using System;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.UserQueries;

/// <summary>
/// Unit tests for the <see cref="UserQuery"/> aggregate's message invariant and for
/// <see cref="UserQueryManager.CreateAsync"/>, which stamps the id and persists. The manager
/// is built by hand with a substituted repository; its GuidGenerator is wired through a
/// substituted <see cref="IAbpLazyServiceProvider"/> returning a fixed id, so the test can
/// prove the saved row carries the generated id rather than a default.
/// </summary>
public class UserQueryTests
{
    [Fact]
    public void Constructor_StoresIdAndMessage()
    {
        var id = Guid.NewGuid();

        var query = new UserQuery(id, "TEST-question about scheduling");

        query.Id.ShouldBe(id);
        query.Message.ShouldBe("TEST-question about scheduling");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_BlankMessage_Throws(string? message)
    {
        var ex = Should.Throw<ArgumentException>(() => new UserQuery(Guid.NewGuid(), message!));

        ex.ParamName.ShouldBe("message");
    }

    [Fact]
    public void Constructor_MessageOverMaxLength_Throws()
    {
        var tooLong = new string('q', UserQueryConsts.MessageMaxLength + 1);

        var ex = Should.Throw<ArgumentException>(() => new UserQuery(Guid.NewGuid(), tooLong));

        ex.ParamName.ShouldBe("message");
    }

    [Fact]
    public void Constructor_MessageAtExactMaxLength_IsAccepted()
    {
        var atLimit = new string('q', UserQueryConsts.MessageMaxLength);

        new UserQuery(Guid.NewGuid(), atLimit).Message.Length.ShouldBe(UserQueryConsts.MessageMaxLength);
    }

    [Fact]
    public async Task Manager_CreateAsync_PersistsWithGeneratedIdAndAutoSave()
    {
        var generatedId = Guid.NewGuid();
        var repository = Substitute.For<IRepository<UserQuery, Guid>>();
        UserQuery? inserted = null;
        var autoSave = false;
        repository
            .InsertAsync(Arg.Do<UserQuery>(q => inserted = q), Arg.Do<bool>(a => autoSave = a), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<UserQuery>());
        var manager = BuildManager(repository, generatedId);

        var result = await manager.CreateAsync("TEST-how do I reschedule?");

        result.Id.ShouldBe(generatedId);
        result.Message.ShouldBe("TEST-how do I reschedule?");
        inserted.ShouldBeSameAs(result);
        autoSave.ShouldBeTrue();
    }

    [Fact]
    public async Task Manager_CreateAsync_BlankMessage_ThrowsAndSavesNothing()
    {
        var repository = Substitute.For<IRepository<UserQuery, Guid>>();
        var manager = BuildManager(repository, Guid.NewGuid());

        var ex = await Should.ThrowAsync<ArgumentException>(() => manager.CreateAsync("  "));

        ex.ParamName.ShouldBe("message");
        repository.ReceivedCalls().ShouldBeEmpty();
    }

    private static UserQueryManager BuildManager(IRepository<UserQuery, Guid> repository, Guid nextId)
    {
        var guidGenerator = Substitute.For<IGuidGenerator>();
        guidGenerator.Create().Returns(nextId);
        var lazy = Substitute.For<IAbpLazyServiceProvider>();
        lazy.LazyGetService<IGuidGenerator>(Arg.Any<IGuidGenerator>()).Returns(guidGenerator);
        lazy.LazyGetService<IGuidGenerator>().Returns(guidGenerator);
        lazy.LazyGetRequiredService<IGuidGenerator>().Returns(guidGenerator);

        return new UserQueryManager(repository) { LazyServiceProvider = lazy };
    }
}
