namespace LeanAI.Domain.WeightManagement.Services;

public static class UnitConverter
{
    private const double KgPerLb  = 0.45359237;
    private const double CmPerIn  = 2.54;
    private const double InPerFt  = 12.0;

    public static double KgToLb(double kg) => kg / KgPerLb;

    public static double LbToKg(double lb) => lb * KgPerLb;

    public static double CmToInches(double cm) => cm / CmPerIn;

    public static double InchesToCm(double inches) => inches * CmPerIn;

    public static double FeetInchesToCm(int feet, double inches) =>
        InchesToCm(feet * InPerFt + inches);

    public static (int Feet, double Inches) CmToFeetAndInches(double cm)
    {
        double totalInches = CmToInches(cm);
        int feet = (int)(totalInches / InPerFt);
        double inches = totalInches - feet * InPerFt;
        return (feet, inches);
    }
}
