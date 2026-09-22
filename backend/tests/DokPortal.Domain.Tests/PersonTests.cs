using DokPortal.Domain.Entities;
using Xunit;

namespace DokPortal.Domain.Tests;

public class PersonTests
{
    [Fact]
    public void FullName_CombinesFirstAndLastName()
    {
        var person = new Person
        {
            Id = Guid.NewGuid(),
            FirstName = "Anna",
            LastName = "Maj"
        };

        Assert.Equal("Anna Maj", person.FullName);
    }
}
