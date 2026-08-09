using FluentAssertions;
using SeeSight.Tenant.Domain;

namespace SeeSight.Tenant.UnitTests.Domain;

public sealed class EmployeeTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid CompanyId = Guid.CreateVersion7();

    [Fact]
    public void Create_sets_the_expected_fields_and_normalizes_email()
    {
        var departmentId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();

        var employee = Employee.Create(
            CompanyId, departmentId, userId, "Someone@Example.com", "First", "Last",
            "Engineer", "+1-555-0100", "US", "P1234567", "JFK", Now);

        employee.CompanyId.Should().Be(CompanyId);
        employee.DepartmentId.Should().Be(departmentId);
        employee.UserId.Should().Be(userId);
        employee.Email.Should().Be("someone@example.com");
        employee.FirstName.Should().Be("First");
        employee.LastName.Should().Be("Last");
        employee.JobTitle.Should().Be("Engineer");
        employee.Status.Should().Be(EmployeeStatus.Active);
        employee.CreatedAt.Should().Be(Now);
        employee.DeletedAt.Should().BeNull();
    }

    [Fact]
    public void Create_allows_a_null_department_and_user()
    {
        var employee = Employee.Create(CompanyId, null, null, "someone@example.com", "First", "Last", null, null, null, null, null, Now);

        employee.DepartmentId.Should().BeNull();
        employee.UserId.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_throws_for_missing_email(string? email)
    {
        var act = () => Employee.Create(CompanyId, null, null, email!, "First", "Last", null, null, null, null, null, Now);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null, "Last")]
    [InlineData("First", null)]
    public void Create_throws_for_missing_name_parts(string? firstName, string? lastName)
    {
        var act = () => Employee.Create(CompanyId, null, null, "someone@example.com", firstName!, lastName!, null, null, null, null, null, Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateProfile_replaces_the_editable_fields()
    {
        var employee = Employee.Create(CompanyId, null, null, "someone@example.com", "First", "Last", null, null, null, null, null, Now);
        var newDepartmentId = Guid.CreateVersion7();

        employee.UpdateProfile("New", "Name", newDepartmentId, "Manager", "+1-555-0199", "CA", "P9999999", "YYZ", Now.AddHours(1));

        employee.FirstName.Should().Be("New");
        employee.LastName.Should().Be("Name");
        employee.DepartmentId.Should().Be(newDepartmentId);
        employee.JobTitle.Should().Be("Manager");
        employee.UpdatedAt.Should().Be(Now.AddHours(1));
    }

    [Fact]
    public void ClearDepartment_sets_DepartmentId_to_null()
    {
        var departmentId = Guid.CreateVersion7();
        var employee = Employee.Create(CompanyId, departmentId, null, "someone@example.com", "First", "Last", null, null, null, null, null, Now);

        employee.ClearDepartment(Now.AddHours(1));

        employee.DepartmentId.Should().BeNull();
        employee.UpdatedAt.Should().Be(Now.AddHours(1));
    }

    [Fact]
    public void Deactivate_and_Activate_toggle_status()
    {
        var employee = Employee.Create(CompanyId, null, null, "someone@example.com", "First", "Last", null, null, null, null, null, Now);

        employee.Deactivate(Now.AddHours(1));
        employee.Status.Should().Be(EmployeeStatus.Inactive);

        employee.Activate(Now.AddHours(2));
        employee.Status.Should().Be(EmployeeStatus.Active);
    }

    [Fact]
    public void Tombstone_mangles_the_email_unlinks_the_user_and_soft_deletes()
    {
        var userId = Guid.CreateVersion7();
        var employee = Employee.Create(CompanyId, null, userId, "someone@example.com", "First", "Last", null, null, null, null, null, Now);

        employee.Tombstone(Now.AddHours(1));

        employee.Email.Should().NotBe("someone@example.com");
        employee.Email.Should().Contain(employee.Id.ToString("N"));
        employee.UserId.Should().BeNull();
        employee.DeletedAt.Should().Be(Now.AddHours(1));
    }
}
