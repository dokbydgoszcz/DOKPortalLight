namespace DokPortal.Domain.Enums;

/// <summary>Funkcja osoby. Wartości liczbowe są zapisane w bazie – nie zmieniać istniejących.</summary>
public enum FunctionType
{
    Catechist = 1,
    Acolyte = 2,
    Lector = 3,
    /// <summary>Proboszcz; funkcja może wskazywać parafię, którą kieruje.</summary>
    Pastor = 4
}
