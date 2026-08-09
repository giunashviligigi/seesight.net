using FluentAssertions;
using SeeSight.Tenant.Domain;

namespace SeeSight.Tenant.UnitTests.Domain;

public sealed class DepartmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid CompanyId = Guid.CreateVersion7();

    [Fact]
    public void Create_sets_the_expected_fields()
    {
        var department = Department.Create(CompanyId, "Engineering", "ENG", Now);

        department.CompanyId.Should().Be(CompanyId);
        department.Name.Should().Be("Engineering");
        department.Code.Should().Be("ENG");
        department.CreatedAt.Should().Be(Now);
        department.UpdatedAt.Should().Be(Now);
        department.DeletedAt.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_throws_for_missing_name(string? name)
    {
        var act = () => Department.Create(CompanyId, name!, null, Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateProfile_replaces_name_and_code()
    {
        var department = Department.Create(CompanyId, "Engineering", "ENG", Now);

        department.UpdateProfile("Product Engineering", "PENG", Now.AddHours(1));

        department.Name.Should().Be("Product Engineering");
        department.Code.Should().Be("PENG");
        department.UpdatedAt.Should().Be(Now.AddHours(1));
    }

    [Fact]
    public void Delete_sets_DeletedAt()
    {
        var department = Department.Create(CompanyId, "Engineering", "ENG", Now);

        department.Delete(Now.AddHours(1));

        department.DeletedAt.Should().Be(Now.AddHours(1));
    }
}
