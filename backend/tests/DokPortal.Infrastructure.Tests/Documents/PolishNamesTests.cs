using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Documents;
using Xunit;
using C = DokPortal.Infrastructure.Documents.GrammaticalCase;

namespace DokPortal.Infrastructure.Tests.Documents;

/// <summary>Odmiana imion i nazwisk w pismach – na nazwiskach z list SKŚP i typowych imionach.</summary>
public class PolishNamesTests
{
    // mianownik | dopełniacz | celownik | biernik | narzędnik | miejscownik | wołacz
    [Theory]
    [InlineData("Jan", "Kowalski", "Jana Kowalskiego", "Janowi Kowalskiemu", "Jana Kowalskiego", "Janem Kowalskim", "Janie Kowalskim", "Janie Kowalski")]
    [InlineData("Szymon", "Łuczak", "Szymona Łuczaka", "Szymonowi Łuczakowi", "Szymona Łuczaka", "Szymonem Łuczakiem", "Szymonie Łuczaku", "Szymonie Łuczaku")]
    [InlineData("Marek", "Dłuski", "Marka Dłuskiego", "Markowi Dłuskiemu", "Marka Dłuskiego", "Markiem Dłuskim", "Marku Dłuskim", "Marku Dłuski")]
    [InlineData("Paweł", "Wróbel", "Pawła Wróbla", "Pawłowi Wróblowi", "Pawła Wróbla", "Pawłem Wróblem", "Pawle Wróblu", "Pawle Wróblu")]
    [InlineData("Piotr", "Holc", "Piotra Holca", "Piotrowi Holcowi", "Piotra Holca", "Piotrem Holcem", "Piotrze Holcu", "Piotrze Holcu")]
    [InlineData("Robert", "Piątkowski", "Roberta Piątkowskiego", "Robertowi Piątkowskiemu", "Roberta Piątkowskiego", "Robertem Piątkowskim", "Robercie Piątkowskim", "Robercie Piątkowski")]
    [InlineData("Dariusz", "Skrzypek", "Dariusza Skrzypka", "Dariuszowi Skrzypkowi", "Dariusza Skrzypka", "Dariuszem Skrzypkiem", "Dariuszu Skrzypku", "Dariuszu Skrzypku")]
    [InlineData("Arkadiusz", "Kula", "Arkadiusza Kuli", "Arkadiuszowi Kuli", "Arkadiusza Kulę", "Arkadiuszem Kulą", "Arkadiuszu Kuli", "Arkadiuszu Kulo")]
    [InlineData("Jerzy", "Czarnecki", "Jerzego Czarneckiego", "Jerzemu Czarneckiemu", "Jerzego Czarneckiego", "Jerzym Czarneckim", "Jerzym Czarneckim", "Jerzy Czarnecki")]
    [InlineData("Antoni", "Nowak", "Antoniego Nowaka", "Antoniemu Nowakowi", "Antoniego Nowaka", "Antonim Nowakiem", "Antonim Nowaku", "Antoni Nowaku")]
    [InlineData("Dawid", "Majczyk", "Dawida Majczyka", "Dawidowi Majczykowi", "Dawida Majczyka", "Dawidem Majczykiem", "Dawidzie Majczyku", "Dawidzie Majczyku")]
    [InlineData("Kazimierz", "Tubisz", "Kazimierza Tubisza", "Kazimierzowi Tubiszowi", "Kazimierza Tubisza", "Kazimierzem Tubiszem", "Kazimierzu Tubiszu", "Kazimierzu Tubiszu")]
    [InlineData("Andrzej", "Kościuszko", "Andrzeja Kościuszki", "Andrzejowi Kościuszce", "Andrzeja Kościuszkę", "Andrzejem Kościuszką", "Andrzeju Kościuszce", "Andrzeju Kościuszko")]
    [InlineData("Michał", "Tafliński", "Michała Taflińskiego", "Michałowi Taflińskiemu", "Michała Taflińskiego", "Michałem Taflińskim", "Michale Taflińskim", "Michale Tafliński")]
    [InlineData("Bronisław", "Noculak", "Bronisława Noculaka", "Bronisławowi Noculakowi", "Bronisława Noculaka", "Bronisławem Noculakiem", "Bronisławie Noculaku", "Bronisławie Noculaku")]
    [InlineData("Radosław", "Maksimczyk", "Radosława Maksimczyka", "Radosławowi Maksimczykowi", "Radosława Maksimczyka", "Radosławem Maksimczykiem", "Radosławie Maksimczyku", "Radosławie Maksimczyku")]
    [InlineData("Wojciech", "Retman", "Wojciecha Retmana", "Wojciechowi Retmanowi", "Wojciecha Retmana", "Wojciechem Retmanem", "Wojciechu Retmanie", "Wojciechu Retmanie")]
    [InlineData("Kuba", "Bocheń", "Kuby Bochenia", "Kubie Bocheniowi", "Kubę Bochenia", "Kubą Bocheniem", "Kubie Bocheniu", "Kubo Bocheniu")]
    public void MaleNames(string first, string last, string gen, string dat, string acc, string ins, string loc, string voc)
    {
        Assert.Equal(PersonSex.Male, PolishNames.InferSex(first));
        Assert.Equal($"{first} {last}", PolishNames.FullName(first, last, PersonSex.Male, C.Nominative));
        Assert.Equal(new[] { gen, dat, acc, ins, loc, voc },
            new[] { C.Genitive, C.Dative, C.Accusative, C.Instrumental, C.Locative, C.Vocative }.Select(c => PolishNames.FullName(first, last, PersonSex.Male, c)));
    }

    [Theory]
    [InlineData("Anna", "Kowalska", "Anny Kowalskiej", "Annie Kowalskiej", "Annę Kowalską", "Anną Kowalską", "Annie Kowalskiej", "Anno Kowalska")]
    [InlineData("Joanna", "Ambrozik", "Joanny Ambrozik", "Joannie Ambrozik", "Joannę Ambrozik", "Joanną Ambrozik", "Joannie Ambrozik", "Joanno Ambrozik")]
    [InlineData("Magdalena", "Burzych", "Magdaleny Burzych", "Magdalenie Burzych", "Magdalenę Burzych", "Magdaleną Burzych", "Magdalenie Burzych", "Magdaleno Burzych")]
    [InlineData("Maria", "Griner", "Marii Griner", "Marii Griner", "Marię Griner", "Marią Griner", "Marii Griner", "Mario Griner")]
    [InlineData("Weronika", "Kula", "Weroniki Kuli", "Weronice Kuli", "Weronikę Kulę", "Weroniką Kulą", "Weronice Kuli", "Weroniko Kulo")]
    [InlineData("Kamilla", "Bzdawka-Piątkowska", "Kamilli Bzdawki-Piątkowskiej", "Kamilli Bzdawce-Piątkowskiej", "Kamillę Bzdawkę-Piątkowską", "Kamillą Bzdawką-Piątkowską", "Kamilli Bzdawce-Piątkowskiej", "Kamillo Bzdawko-Piątkowska")]
    [InlineData("Benedykta", "Holc", "Benedykty Holc", "Benedykcie Holc", "Benedyktę Holc", "Benedyktą Holc", "Benedykcie Holc", "Benedykto Holc")]
    [InlineData("Małgorzata", "Baranowska", "Małgorzaty Baranowskiej", "Małgorzacie Baranowskiej", "Małgorzatę Baranowską", "Małgorzatą Baranowską", "Małgorzacie Baranowskiej", "Małgorzato Baranowska")]
    [InlineData("Barbara", "Nowak", "Barbary Nowak", "Barbarze Nowak", "Barbarę Nowak", "Barbarą Nowak", "Barbarze Nowak", "Barbaro Nowak")]
    [InlineData("Ludmiła", "Braune", "Ludmiły Braune", "Ludmile Braune", "Ludmiłę Braune", "Ludmiłą Braune", "Ludmile Braune", "Ludmiło Braune")]
    [InlineData("Jadwiga", "Szweda", "Jadwigi Szwedy", "Jadwidze Szwedzie", "Jadwigę Szwedę", "Jadwigą Szwedą", "Jadwidze Szwedzie", "Jadwigo Szwedo")]
    [InlineData("Alicja", "Tobijańska", "Alicji Tobijańskiej", "Alicji Tobijańskiej", "Alicję Tobijańską", "Alicją Tobijańską", "Alicji Tobijańskiej", "Alicjo Tobijańska")]
    [InlineData("Kasia", "Dąbrowska", "Kasi Dąbrowskiej", "Kasi Dąbrowskiej", "Kasię Dąbrowską", "Kasią Dąbrowską", "Kasi Dąbrowskiej", "Kasiu Dąbrowska")]
    [InlineData("Maja", "Kowalewska", "Mai Kowalewskiej", "Mai Kowalewskiej", "Maję Kowalewską", "Mają Kowalewską", "Mai Kowalewskiej", "Majo Kowalewska")]
    [InlineData("Lucyna", "Stachowska", "Lucyny Stachowskiej", "Lucynie Stachowskiej", "Lucynę Stachowską", "Lucyną Stachowską", "Lucynie Stachowskiej", "Lucyno Stachowska")]
    [InlineData("Teresa", "Schalau", "Teresy Schalau", "Teresie Schalau", "Teresę Schalau", "Teresą Schalau", "Teresie Schalau", "Tereso Schalau")]
    [InlineData("Marta", "Kościuszko", "Marty Kościuszko", "Marcie Kościuszko", "Martę Kościuszko", "Martą Kościuszko", "Marcie Kościuszko", "Marto Kościuszko")]
    [InlineData("Ola", "Kowalczykowa", "Oli Kowalczykowej", "Oli Kowalczykowej", "Olę Kowalczykową", "Olą Kowalczykową", "Oli Kowalczykowej", "Olu Kowalczykowa")]
    [InlineData("Noemi", "Lis", "Noemi Lis", "Noemi Lis", "Noemi Lis", "Noemi Lis", "Noemi Lis", "Noemi Lis")]
    public void FemaleNames(string first, string last, string gen, string dat, string acc, string ins, string loc, string voc)
    {
        Assert.Equal(PersonSex.Female, PolishNames.InferSex(first));
        Assert.Equal(new[] { gen, dat, acc, ins, loc, voc },
            new[] { C.Genitive, C.Dative, C.Accusative, C.Instrumental, C.Locative, C.Vocative }.Select(c => PolishNames.FullName(first, last, PersonSex.Female, c)));
    }

    [Fact]
    public void SeveralFirstNames_CapitalLetters_AndEdgeCases()
    {
        Assert.Equal("Marii Anny Nowak", PolishNames.FullName("Maria Anna", "Nowak", PersonSex.Female, C.Genitive));
        Assert.Equal("Jana KOWALSKIEGO", PolishNames.FullName("Jan", "KOWALSKI", PersonSex.Male, C.Genitive));
        Assert.Equal("Bruno", PolishNames.FirstName("Bruno", PersonSex.Male, C.Genitive));
        Assert.Equal("Linde", PolishNames.LastName("Linde", PersonSex.Male, C.Genitive));
        Assert.Equal(PersonSex.Male, PolishNames.InferSex(""));
        Assert.Equal(PersonSex.Male, PolishNames.InferSex("Barnaba"));
        Assert.Equal(PersonSex.Female, PolishNames.InferSex("EWA"));
        Assert.Equal("", PolishNames.FullName("", "", PersonSex.Male, C.Dative));
        Assert.True(PolishNames.CaseNames.ContainsKey("dopełniacz"));
        Assert.Equal(GrammaticalCase.Instrumental, PolishNames.CaseNames["NARZĘDNIK"]);
    }
}
