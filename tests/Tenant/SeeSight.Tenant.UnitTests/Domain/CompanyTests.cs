using FluentAssertions;
using SeeSight.Tenant.Domain;

namespace SeeSight.Tenant.UnitTests.Domain;

public sealed class CompanyTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_sets_the_expected_defaults()
    {
        var company = Company.Create("Acme Corp", "Acme Corporation Ltd", "US", "billing@acme.com", "America/New_York", null, Now);

        company.Name.Should().Be("Acme Corp");
        company.LegalName.Should().Be("Acme Corporation Ltd");
        company.Country.Should().Be("US");
        company.BillingEmail.Should().Be("billing@acme.com");
        company.Timezone.Should().Be("America/New_York");
        company.Status.Should().Be(CompanyStatus.Active);
        company.DeletedAt.Should().BeNull();
        company.CreatedAt.Should().Be(Now);
        company.UpdatedAt.Should().Be(Now);
        company.IsUsable.Should().BeTrue();
    }

    [Fact]
    public void Create_generates_a_non_empty_unique_slug()
    {
        var first = Company.Create("Acme Corp", null, null, null, "UTC", null, Now);
        var second = Company.Create("Acme Corp", null, null, null, "UTC", null, Now);

        first.Slug.Should().NotBeNullOrWhiteSpace();
        first.Slug.Should().NotBe(second.Slug, "two companies with the same name must still get distinct slugs");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_throws_for_missing_name(string? name)
    {
        var act = () => Company.Create(name!, null, null, null, "UTC", null, Now);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_throws_for_missing_timezone(string? timezone)
    {
        var act = () => Company.Create("Acme Corp", null, null, null, timezone!, null, Now);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateProfile_replaces_the_editable_fields()
    {
        var company = Company.Create("Acme Corp", null, null, null, "UTC", null, Now);

        company.UpdateProfile("New Name", "New Legal Name", "CA", "new@acme.com", "America/Toronto", "{\"x\":1}", Now.AddHours(1));

        company.Name.Should().Be("New Name");
        company.LegalName.Should().Be("New Legal Name");
        company.Country.Should().Be("CA");
        company.BillingEmail.Should().Be("new@acme.com");
        company.Timezone.Should().Be("America/Toronto");
        company.PolicyJson.Should().Be("{\"x\":1}");
        company.UpdatedAt.Should().Be(Now.AddHours(1));
    }

    [Fact]
    public void Deactivate_sets_status_inactive()
    {
        var company = Company.Create("Acme Corp", null, null, null, "UTC", null, Now);

        company.Deactivate(Now.AddHours(1));

        company.Status.Should().Be(CompanyStatus.Inactive);
        company.IsUsable.Should().BeFalse();
    }

    [Fact]
    public void Activate_sets_status_active_and_clears_a_prior_soft_delete()
    {
        var company = Company.Create("Acme Corp", null, null, null, "UTC", null, Now);
        company.Deactivate(Now.AddHours(1));
        company.Delete(Now.AddHours(2));

        company.Activate(Now.AddHours(3));

        company.Status.Should().Be(CompanyStatus.Active);
        company.DeletedAt.Should().BeNull();
        company.IsUsable.Should().BeTrue();
    }

    [Fact]
    public void Delete_sets_DeletedAt()
    {
        var company = Company.Create("Acme Corp", null, null, null, "UTC", null, Now);

        company.Delete(Now.AddHours(1));

        company.DeletedAt.Should().Be(Now.AddHours(1));
        company.IsUsable.Should().BeFalse();
    }
}
