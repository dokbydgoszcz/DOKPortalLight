using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DokPortal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedBydgoszczDioceseParishes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove the 3 placeholder parishes created by DbSeeder before this migration existed.
            migrationBuilder.Sql(@"DELETE FROM [Parishes] WHERE [City] IS NULL AND [Name] IN (N'św. Mateusza', N'Chrystusa Króla', N'św. Józefa');");

            // Full parish list for the Diocese of Bydgoszcz, sourced from the official
            // diocese website (diecezja.bydgoszcz.pl/2009/02/21/spis-dekanatow-i-parafii).
            migrationBuilder.Sql(@"INSERT INTO [Parishes] ([Id], [Name], [City])
VALUES
    (NEWID(), N'Chrystusa Dobrego Pasterza', N'Białe Błota'),
    (NEWID(), N'Matki Boskiej Bolesnej', N'Ciele'),
    (NEWID(), N'św. Jakuba Mniejszego Apostoła', N'Dąbrówka Nowa'),
    (NEWID(), N'św. Kazimierza Królewicza', N'Kruszyn'),
    (NEWID(), N'św. Kazimierza', N'Łochowo'),
    (NEWID(), N'św. Rafała Kalinowskiego', N'Murowaniec'),
    (NEWID(), N'Wniebowzięcia NMP', N'Przyłęki'),
    (NEWID(), N'św. Andrzeja Boboli', N'Sicienko'),
    (NEWID(), N'św. Michała Archanioła', N'Wtelno'),
    (NEWID(), N'św. Antoniego z Padwy', N'Bydgoszcz'),
    (NEWID(), N'św. Maksymiliana Kolbego', N'Bydgoszcz'),
    (NEWID(), N'bł. Michała Kozala Biskupa i Męczennika', N'Bydgoszcz'),
    (NEWID(), N'Niepokalanego Poczęcia NMP', N'Bydgoszcz'),
    (NEWID(), N'NMP z Góry Karmel', N'Bydgoszcz'),
    (NEWID(), N'Przemienienia Pańskiego', N'Bydgoszcz'),
    (NEWID(), N'Świętej Rodziny', N'Bydgoszcz'),
    (NEWID(), N'św. Urszuli Ledóchowskiej', N'Bydgoszcz'),
    (NEWID(), N'Wniebowstąpienia Pańskiego', N'Bydgoszcz'),
    (NEWID(), N'św. Andrzeja Boboli', N'Bydgoszcz'),
    (NEWID(), N'Świętego Krzyża', N'Bydgoszcz'),
    (NEWID(), N'Katedralna pw. św. Marcina i Mikołaja', N'Bydgoszcz'),
    (NEWID(), N'Najświętszego Serca Pana Jezusa', N'Bydgoszcz'),
    (NEWID(), N'Świętych Apostołów Piotra i Pawła', N'Bydgoszcz'),
    (NEWID(), N'św. Wincentego á Paulo', N'Bydgoszcz'),
    (NEWID(), N'NMP Królowej Pokoju (kościół garnizonowy)', N'Bydgoszcz'),
    (NEWID(), N'Bożego Ciała', N'Bydgoszcz'),
    (NEWID(), N'Chrystusa Króla', N'Bydgoszcz'),
    (NEWID(), N'Matki Boskiej Nieustającej Pomocy', N'Bydgoszcz'),
    (NEWID(), N'Miłosierdzia Bożego', N'Bydgoszcz'),
    (NEWID(), N'NMP Matki Kościoła', N'Bydgoszcz'),
    (NEWID(), N'Świętej Trójcy', N'Bydgoszcz'),
    (NEWID(), N'św. Wojciecha', N'Bydgoszcz'),
    (NEWID(), N'Ducha Świętego', N'Bydgoszcz'),
    (NEWID(), N'św. Jadwigi Królowej', N'Bydgoszcz'),
    (NEWID(), N'Matki Bożej Fatimskiej', N'Bydgoszcz'),
    (NEWID(), N'Najświętszej Maryi Panny Królowej Polski', N'Bydgoszcz'),
    (NEWID(), N'Opatrzności Bożej', N'Bydgoszcz'),
    (NEWID(), N'Świętych Polskich Braci Męczenników', N'Bydgoszcz'),
    (NEWID(), N'bł. Michała Kozala', N'Solec Kujawski'),
    (NEWID(), N'Najświętszego Serca Pana Jezusa', N'Solec Kujawski'),
    (NEWID(), N'Nawrócenia św. Pawła', N'Solec Kujawski'),
    (NEWID(), N'św. Stanisława Biskupa i Męczennika', N'Solec Kujawski'),
    (NEWID(), N'św. Jana Apostoła i Ewangelisty', N'Bydgoszcz'),
    (NEWID(), N'św. Jana Pawła II', N'Bydgoszcz'),
    (NEWID(), N'św. Łukasza Ewangelisty', N'Bydgoszcz'),
    (NEWID(), N'św. Marka', N'Bydgoszcz'),
    (NEWID(), N'św. Mateusza Apostoła i Ewangelisty', N'Bydgoszcz'),
    (NEWID(), N'Matki Boskiej Królowej Męczenników', N'Bydgoszcz'),
    (NEWID(), N'św. Mikołaja', N'Bydgoszcz'),
    (NEWID(), N'św. Józefa', N'Bydgoszcz'),
    (NEWID(), N'Matki Boskiej Częstochowskiej', N'Bydgoszcz'),
    (NEWID(), N'Matki Boskiej Ostrobramskiej', N'Bydgoszcz'),
    (NEWID(), N'Matki Bożej Zwycięskiej', N'Bydgoszcz'),
    (NEWID(), N'św. Stanisława Biskupa i Męczennika', N'Bydgoszcz'),
    (NEWID(), N'Zmartwychwstania Pańskiego', N'Bydgoszcz'),
    (NEWID(), N'św. Małgorzaty', N'Chojna'),
    (NEWID(), N'św. Andrzeja Apostoła', N'Czeszewo'),
    (NEWID(), N'św. Jakuba Apostoła', N'Dziewierzewo'),
    (NEWID(), N'św. Wawrzyńca', N'Gołańcz'),
    (NEWID(), N'św. Anny', N'Jaktorowo'),
    (NEWID(), N'św. Michała Archanioła', N'Kcynia'),
    (NEWID(), N'Wniebowzięcia NMP', N'Kcynia'),
    (NEWID(), N'św. Jana Chrzciciela', N'Panigródz'),
    (NEWID(), N'św. Jana Nepomucena', N'Sipiory'),
    (NEWID(), N'św. Katarzyny', N'Smogulec'),
    (NEWID(), N'NMP Królowej Polski', N'Brzoza'),
    (NEWID(), N'św. Apostołów Piotra i Pawła', N'Chomętowo'),
    (NEWID(), N'św. Mikołaja', N'Łabiszyn'),
    (NEWID(), N'Zwiastowania NMP', N'Łabiszyn'),
    (NEWID(), N'św. Katarzyny', N'Rynarzewo'),
    (NEWID(), N'Przemienienia Pańskiego', N'Władysławowo'),
    (NEWID(), N'św. Ojca Pio', N'Zamość'),
    (NEWID(), N'św. Jakuba Większego Apostoła', N'Bługowo'),
    (NEWID(), N'św. Mikołaja', N'Dźwierszno Wielkie'),
    (NEWID(), N'św. Józefa', N'Fanianowo'),
    (NEWID(), N'NMP Niepokalanie Poczętej', N'Górka Klasztorna'),
    (NEWID(), N'św. Jakuba Większego', N'Gromadno'),
    (NEWID(), N'Świętej Trójcy', N'Łobżenica'),
    (NEWID(), N'św. Andrzeja Boboli', N'Radzicz'),
    (NEWID(), N'Świętej Trójcy', N'Runowo Krajeńskie'),
    (NEWID(), N'św. Mikołaja', N'Tłukomy'),
    (NEWID(), N'św. Anny', N'Drzewianowo'),
    (NEWID(), N'św. Mikołaja', N'Mrocza'),
    (NEWID(), N'św. Macieja', N'Orle'),
    (NEWID(), N'Najświętszego Serca Pana Jezusa', N'Samsieczno'),
    (NEWID(), N'Świętych Apostołów Piotra i Pawła', N'Wierzchucin Królewski'),
    (NEWID(), N'Matki Boskiej Szkaplerznej', N'Wierzchucinek'),
    (NEWID(), N'św. Jakuba Apostoła', N'Zabartowo'),
    (NEWID(), N'św. Michała Archanioła', N'Dębowo'),
    (NEWID(), N'NMP Królowej Polski', N'Nakło'),
    (NEWID(), N'św. Stanisława Biskupa i Męczennika', N'Nakło'),
    (NEWID(), N'św. Wawrzyńca', N'Nakło'),
    (NEWID(), N'Matki Boskiej Bolesnej', N'Paterek'),
    (NEWID(), N'Zwiastowania NMP', N'Potulice'),
    (NEWID(), N'św. Wojciecha', N'Sadki'),
    (NEWID(), N'św. Mikołaja', N'Ślesin'),
    (NEWID(), N'bł. Czesława', N'Śmielin'),
    (NEWID(), N'św. Brata Alberta Chmielowskiego', N'Występ'),
    (NEWID(), N'św. Wawrzyńca', N'Dobrcz'),
    (NEWID(), N'św. Alberta Chmielowskiego', N'Kotomierz'),
    (NEWID(), N'św. Maksymiliana Kolbe', N'Maksymilianowo'),
    (NEWID(), N'Matki Bożej Wspomożenia Wiernych', N'Niemcz'),
    (NEWID(), N'Narodzenia NMP', N'Osielsko'),
    (NEWID(), N'św. Stanisława Kostki', N'Strzelce Górne'),
    (NEWID(), N'Matki Bożej Królowej Polski', N'Włóki'),
    (NEWID(), N'Podwyższenia Krzyża Świętego', N'Żołędowo'),
    (NEWID(), N'św. Wawrzyńca', N'Lutowo'),
    (NEWID(), N'św. Maksymiliana Kolbego', N'Pęperzyn'),
    (NEWID(), N'św. Bartłomieja Apostoła', N'Sępólno Krajeńskie'),
    (NEWID(), N'św. Józefa', N'Sitno'),
    (NEWID(), N'św. Katarzyny Aleksandryjskiej', N'Sypniewo'),
    (NEWID(), N'św. Marii Magdaleny', N'Wąwelno'),
    (NEWID(), N'św. Jakuba Apostoła', N'Wielowicz'),
    (NEWID(), N'Wniebowzięcia NMP i Świętych Apostołów Szymona i Judy Tadeusza', N'Więcbork'),
    (NEWID(), N'św. Wojciecha', N'Kołaczkowo'),
    (NEWID(), N'św. Bartłomieja Apostoła', N'Samoklęski Duże'),
    (NEWID(), N'św. Wita', N'Słupy'),
    (NEWID(), N'św. Mikołaja Biskupa', N'Szaradowo'),
    (NEWID(), N'św. Andrzeja Boboli', N'Szubin'),
    (NEWID(), N'św. Marcina Biskupa', N'Szubin'),
    (NEWID(), N'św. Stanisława Kostki', N'Tur'),
    (NEWID(), N'Najświętszego Serca Pana Jezusa', N'Białośliwie'),
    (NEWID(), N'św. Jadwigi', N'Glesno'),
    (NEWID(), N'św. Anny', N'Kosztowo'),
    (NEWID(), N'św. Mikołaja i Błogosławionego Biskupa Michała Kozala', N'Krostkowo'),
    (NEWID(), N'Niepokalanego Poczęcia NMP', N'Nieżychowo'),
    (NEWID(), N'św. Józefa', N'Osiek'),
    (NEWID(), N'św. Marcina Biskupa i Męczennika', N'Wyrzysk'),
    (NEWID(), N'Przenajdroższej Krwi Pana Naszego Jezusa Chrystusa', N'Żelazno'),
    (NEWID(), N'św. Józefa', N'Bądecz'),
    (NEWID(), N'Świętych Piotra i Pawła', N'Dziembowo'),
    (NEWID(), N'św. Antoniego z Padwy', N'Grabówno'),
    (NEWID(), N'św. Andrzeja Boboli', N'Kaczory'),
    (NEWID(), N'Podwyższenia Krzyża Świętego', N'Miasteczko Krajeńskie'),
    (NEWID(), N'Przemienienia Pańskiego', N'Morzewo'),
    (NEWID(), N'Matki Boskiej Anielskiej', N'Rzadkowo'),
    (NEWID(), N'św. Małgorzaty P. M. i Matki Bożej Szkaplerznej', N'Śmiłowo'),
    (NEWID(), N'NMP Różańcowej', N'Wysoka'),
    (NEWID(), N'św. Katarzyny', N'Lipka'),
    (NEWID(), N'św. Barbary', N'Radawnica'),
    (NEWID(), N'św. Marcina Biskupa', N'Stara Wiśniewka'),
    (NEWID(), N'Świętej Trójcy', N'Wielki Buczek'),
    (NEWID(), N'św. Marii Magdaleny', N'Zakrzewo'),
    (NEWID(), N'św. Piotra i Pawła Apostołów', N'Złotów'),
    (NEWID(), N'Wniebowzięcia NMP', N'Złotów'),
    (NEWID(), N'Trójcy Świętej', N'Głubczyn'),
    (NEWID(), N'św. Anny', N'Krajenka'),
    (NEWID(), N'św. Jakuba Apostoła', N'Sławianowo'),
    (NEWID(), N'św. Jana Chrzciciela', N'Święta'),
    (NEWID(), N'św. Rocha', N'Złotów');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM [Parishes]
WHERE ([Name] = N'Chrystusa Dobrego Pasterza' AND [City] = N'Białe Błota')
   OR ([Name] = N'Matki Boskiej Bolesnej' AND [City] = N'Ciele')
   OR ([Name] = N'św. Jakuba Mniejszego Apostoła' AND [City] = N'Dąbrówka Nowa')
   OR ([Name] = N'św. Kazimierza Królewicza' AND [City] = N'Kruszyn')
   OR ([Name] = N'św. Kazimierza' AND [City] = N'Łochowo')
   OR ([Name] = N'św. Rafała Kalinowskiego' AND [City] = N'Murowaniec')
   OR ([Name] = N'Wniebowzięcia NMP' AND [City] = N'Przyłęki')
   OR ([Name] = N'św. Andrzeja Boboli' AND [City] = N'Sicienko')
   OR ([Name] = N'św. Michała Archanioła' AND [City] = N'Wtelno')
   OR ([Name] = N'św. Antoniego z Padwy' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'św. Maksymiliana Kolbego' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'bł. Michała Kozala Biskupa i Męczennika' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'Niepokalanego Poczęcia NMP' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'NMP z Góry Karmel' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'Przemienienia Pańskiego' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'Świętej Rodziny' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'św. Urszuli Ledóchowskiej' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'Wniebowstąpienia Pańskiego' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'św. Andrzeja Boboli' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'Świętego Krzyża' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'Katedralna pw. św. Marcina i Mikołaja' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'Najświętszego Serca Pana Jezusa' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'Świętych Apostołów Piotra i Pawła' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'św. Wincentego á Paulo' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'NMP Królowej Pokoju (kościół garnizonowy)' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'Bożego Ciała' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'Chrystusa Króla' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'Matki Boskiej Nieustającej Pomocy' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'Miłosierdzia Bożego' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'NMP Matki Kościoła' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'Świętej Trójcy' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'św. Wojciecha' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'Ducha Świętego' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'św. Jadwigi Królowej' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'Matki Bożej Fatimskiej' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'Najświętszej Maryi Panny Królowej Polski' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'Opatrzności Bożej' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'Świętych Polskich Braci Męczenników' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'bł. Michała Kozala' AND [City] = N'Solec Kujawski')
   OR ([Name] = N'Najświętszego Serca Pana Jezusa' AND [City] = N'Solec Kujawski')
   OR ([Name] = N'Nawrócenia św. Pawła' AND [City] = N'Solec Kujawski')
   OR ([Name] = N'św. Stanisława Biskupa i Męczennika' AND [City] = N'Solec Kujawski')
   OR ([Name] = N'św. Jana Apostoła i Ewangelisty' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'św. Jana Pawła II' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'św. Łukasza Ewangelisty' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'św. Marka' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'św. Mateusza Apostoła i Ewangelisty' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'Matki Boskiej Królowej Męczenników' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'św. Mikołaja' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'św. Józefa' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'Matki Boskiej Częstochowskiej' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'Matki Boskiej Ostrobramskiej' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'Matki Bożej Zwycięskiej' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'św. Stanisława Biskupa i Męczennika' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'Zmartwychwstania Pańskiego' AND [City] = N'Bydgoszcz')
   OR ([Name] = N'św. Małgorzaty' AND [City] = N'Chojna')
   OR ([Name] = N'św. Andrzeja Apostoła' AND [City] = N'Czeszewo')
   OR ([Name] = N'św. Jakuba Apostoła' AND [City] = N'Dziewierzewo')
   OR ([Name] = N'św. Wawrzyńca' AND [City] = N'Gołańcz')
   OR ([Name] = N'św. Anny' AND [City] = N'Jaktorowo')
   OR ([Name] = N'św. Michała Archanioła' AND [City] = N'Kcynia')
   OR ([Name] = N'Wniebowzięcia NMP' AND [City] = N'Kcynia')
   OR ([Name] = N'św. Jana Chrzciciela' AND [City] = N'Panigródz')
   OR ([Name] = N'św. Jana Nepomucena' AND [City] = N'Sipiory')
   OR ([Name] = N'św. Katarzyny' AND [City] = N'Smogulec')
   OR ([Name] = N'NMP Królowej Polski' AND [City] = N'Brzoza')
   OR ([Name] = N'św. Apostołów Piotra i Pawła' AND [City] = N'Chomętowo')
   OR ([Name] = N'św. Mikołaja' AND [City] = N'Łabiszyn')
   OR ([Name] = N'Zwiastowania NMP' AND [City] = N'Łabiszyn')
   OR ([Name] = N'św. Katarzyny' AND [City] = N'Rynarzewo')
   OR ([Name] = N'Przemienienia Pańskiego' AND [City] = N'Władysławowo')
   OR ([Name] = N'św. Ojca Pio' AND [City] = N'Zamość')
   OR ([Name] = N'św. Jakuba Większego Apostoła' AND [City] = N'Bługowo')
   OR ([Name] = N'św. Mikołaja' AND [City] = N'Dźwierszno Wielkie')
   OR ([Name] = N'św. Józefa' AND [City] = N'Fanianowo')
   OR ([Name] = N'NMP Niepokalanie Poczętej' AND [City] = N'Górka Klasztorna')
   OR ([Name] = N'św. Jakuba Większego' AND [City] = N'Gromadno')
   OR ([Name] = N'Świętej Trójcy' AND [City] = N'Łobżenica')
   OR ([Name] = N'św. Andrzeja Boboli' AND [City] = N'Radzicz')
   OR ([Name] = N'Świętej Trójcy' AND [City] = N'Runowo Krajeńskie')
   OR ([Name] = N'św. Mikołaja' AND [City] = N'Tłukomy')
   OR ([Name] = N'św. Anny' AND [City] = N'Drzewianowo')
   OR ([Name] = N'św. Mikołaja' AND [City] = N'Mrocza')
   OR ([Name] = N'św. Macieja' AND [City] = N'Orle')
   OR ([Name] = N'Najświętszego Serca Pana Jezusa' AND [City] = N'Samsieczno')
   OR ([Name] = N'Świętych Apostołów Piotra i Pawła' AND [City] = N'Wierzchucin Królewski')
   OR ([Name] = N'Matki Boskiej Szkaplerznej' AND [City] = N'Wierzchucinek')
   OR ([Name] = N'św. Jakuba Apostoła' AND [City] = N'Zabartowo')
   OR ([Name] = N'św. Michała Archanioła' AND [City] = N'Dębowo')
   OR ([Name] = N'NMP Królowej Polski' AND [City] = N'Nakło')
   OR ([Name] = N'św. Stanisława Biskupa i Męczennika' AND [City] = N'Nakło')
   OR ([Name] = N'św. Wawrzyńca' AND [City] = N'Nakło')
   OR ([Name] = N'Matki Boskiej Bolesnej' AND [City] = N'Paterek')
   OR ([Name] = N'Zwiastowania NMP' AND [City] = N'Potulice')
   OR ([Name] = N'św. Wojciecha' AND [City] = N'Sadki')
   OR ([Name] = N'św. Mikołaja' AND [City] = N'Ślesin')
   OR ([Name] = N'bł. Czesława' AND [City] = N'Śmielin')
   OR ([Name] = N'św. Brata Alberta Chmielowskiego' AND [City] = N'Występ')
   OR ([Name] = N'św. Wawrzyńca' AND [City] = N'Dobrcz')
   OR ([Name] = N'św. Alberta Chmielowskiego' AND [City] = N'Kotomierz')
   OR ([Name] = N'św. Maksymiliana Kolbe' AND [City] = N'Maksymilianowo')
   OR ([Name] = N'Matki Bożej Wspomożenia Wiernych' AND [City] = N'Niemcz')
   OR ([Name] = N'Narodzenia NMP' AND [City] = N'Osielsko')
   OR ([Name] = N'św. Stanisława Kostki' AND [City] = N'Strzelce Górne')
   OR ([Name] = N'Matki Bożej Królowej Polski' AND [City] = N'Włóki')
   OR ([Name] = N'Podwyższenia Krzyża Świętego' AND [City] = N'Żołędowo')
   OR ([Name] = N'św. Wawrzyńca' AND [City] = N'Lutowo')
   OR ([Name] = N'św. Maksymiliana Kolbego' AND [City] = N'Pęperzyn')
   OR ([Name] = N'św. Bartłomieja Apostoła' AND [City] = N'Sępólno Krajeńskie')
   OR ([Name] = N'św. Józefa' AND [City] = N'Sitno')
   OR ([Name] = N'św. Katarzyny Aleksandryjskiej' AND [City] = N'Sypniewo')
   OR ([Name] = N'św. Marii Magdaleny' AND [City] = N'Wąwelno')
   OR ([Name] = N'św. Jakuba Apostoła' AND [City] = N'Wielowicz')
   OR ([Name] = N'Wniebowzięcia NMP i Świętych Apostołów Szymona i Judy Tadeusza' AND [City] = N'Więcbork')
   OR ([Name] = N'św. Wojciecha' AND [City] = N'Kołaczkowo')
   OR ([Name] = N'św. Bartłomieja Apostoła' AND [City] = N'Samoklęski Duże')
   OR ([Name] = N'św. Wita' AND [City] = N'Słupy')
   OR ([Name] = N'św. Mikołaja Biskupa' AND [City] = N'Szaradowo')
   OR ([Name] = N'św. Andrzeja Boboli' AND [City] = N'Szubin')
   OR ([Name] = N'św. Marcina Biskupa' AND [City] = N'Szubin')
   OR ([Name] = N'św. Stanisława Kostki' AND [City] = N'Tur')
   OR ([Name] = N'Najświętszego Serca Pana Jezusa' AND [City] = N'Białośliwie')
   OR ([Name] = N'św. Jadwigi' AND [City] = N'Glesno')
   OR ([Name] = N'św. Anny' AND [City] = N'Kosztowo')
   OR ([Name] = N'św. Mikołaja i Błogosławionego Biskupa Michała Kozala' AND [City] = N'Krostkowo')
   OR ([Name] = N'Niepokalanego Poczęcia NMP' AND [City] = N'Nieżychowo')
   OR ([Name] = N'św. Józefa' AND [City] = N'Osiek')
   OR ([Name] = N'św. Marcina Biskupa i Męczennika' AND [City] = N'Wyrzysk')
   OR ([Name] = N'Przenajdroższej Krwi Pana Naszego Jezusa Chrystusa' AND [City] = N'Żelazno')
   OR ([Name] = N'św. Józefa' AND [City] = N'Bądecz')
   OR ([Name] = N'Świętych Piotra i Pawła' AND [City] = N'Dziembowo')
   OR ([Name] = N'św. Antoniego z Padwy' AND [City] = N'Grabówno')
   OR ([Name] = N'św. Andrzeja Boboli' AND [City] = N'Kaczory')
   OR ([Name] = N'Podwyższenia Krzyża Świętego' AND [City] = N'Miasteczko Krajeńskie')
   OR ([Name] = N'Przemienienia Pańskiego' AND [City] = N'Morzewo')
   OR ([Name] = N'Matki Boskiej Anielskiej' AND [City] = N'Rzadkowo')
   OR ([Name] = N'św. Małgorzaty P. M. i Matki Bożej Szkaplerznej' AND [City] = N'Śmiłowo')
   OR ([Name] = N'NMP Różańcowej' AND [City] = N'Wysoka')
   OR ([Name] = N'św. Katarzyny' AND [City] = N'Lipka')
   OR ([Name] = N'św. Barbary' AND [City] = N'Radawnica')
   OR ([Name] = N'św. Marcina Biskupa' AND [City] = N'Stara Wiśniewka')
   OR ([Name] = N'Świętej Trójcy' AND [City] = N'Wielki Buczek')
   OR ([Name] = N'św. Marii Magdaleny' AND [City] = N'Zakrzewo')
   OR ([Name] = N'św. Piotra i Pawła Apostołów' AND [City] = N'Złotów')
   OR ([Name] = N'Wniebowzięcia NMP' AND [City] = N'Złotów')
   OR ([Name] = N'Trójcy Świętej' AND [City] = N'Głubczyn')
   OR ([Name] = N'św. Anny' AND [City] = N'Krajenka')
   OR ([Name] = N'św. Jakuba Apostoła' AND [City] = N'Sławianowo')
   OR ([Name] = N'św. Jana Chrzciciela' AND [City] = N'Święta')
   OR ([Name] = N'św. Rocha' AND [City] = N'Złotów');");
        }
    }
}
