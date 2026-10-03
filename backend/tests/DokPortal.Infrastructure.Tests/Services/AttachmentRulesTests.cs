using DokPortal.Application.Attachments;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class AttachmentRulesTests
{
    [Theory]
    [InlineData("scan.pdf")]
    [InlineData("zdjecie.JPG")]
    [InlineData("zdjecie.jpeg")]
    [InlineData("zrzut.png")]
    [InlineData("pismo.docx")]
    [InlineData("pismo.doc")]
    [InlineData("notatka.TXT")]
    public void Validate_AcceptsTheAllowedTypes(string fileName)
    {
        Assert.Null(AttachmentRules.Validate(fileName, 1024));
    }

    [Theory]
    [InlineData("program.exe")]
    [InlineData("arkusz.xlsx")]
    [InlineData("bez-rozszerzenia")]
    [InlineData("archiwum.pdf.zip")]
    [InlineData("")]
    public void Validate_RejectsOtherTypes_NamingTheAllowedOnes(string fileName)
    {
        var error = AttachmentRules.Validate(fileName, 1024);

        Assert.NotNull(error);
        Assert.Contains("PDF", error);
        Assert.Contains("DOCX", error);
    }

    [Fact]
    public void Validate_RejectsEmptyFiles()
    {
        Assert.Equal("Plik jest pusty.", AttachmentRules.Validate("a.pdf", 0));
    }

    [Fact]
    public void Validate_AcceptsExactlyTheLimit_AndRejectsOneByteMore()
    {
        Assert.Null(AttachmentRules.Validate("a.pdf", AttachmentRules.MaxFileBytes));
        var error = AttachmentRules.Validate("a.pdf", AttachmentRules.MaxFileBytes + 1);

        Assert.NotNull(error);
        Assert.Contains("20 MB", error);
    }

    [Theory]
    [InlineData("a.pdf", "application/pdf")]
    [InlineData("a.JPG", "image/jpeg")]
    [InlineData("a.jpeg", "image/jpeg")]
    [InlineData("a.png", "image/png")]
    [InlineData("a.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("a.doc", "application/msword")]
    [InlineData("a.txt", "text/plain; charset=utf-8")]
    public void ContentTypeFor_IsDerivedFromTheExtension(string fileName, string expected)
    {
        Assert.Equal(expected, AttachmentRules.ContentTypeFor(fileName));
    }

    [Theory]
    [InlineData(@"C:\skany\umowa.pdf", "umowa.pdf")]
    [InlineData("../../etc/passwd.txt", "passwd.txt")]
    [InlineData("  notatka  .txt", "notatka  .txt")]
    public void CleanFileName_KeepsOnlyTheNamePart(string raw, string expected)
    {
        Assert.Equal(expected, AttachmentRules.CleanFileName(raw));
    }
}
