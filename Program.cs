using System.Globalization;

if (args.Length != 2)
{
    Console.WriteLine("Verwendung: dotnet run -- <typ> <wert>");
    Console.WriteLine("Unterstützte Typen: int, double, bool, string");
    return 1;
}

var type = args[0].ToLowerInvariant();
var value = args[1];

switch (type)
{
    case "int":
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
        {
            Console.WriteLine(intValue);
            return 0;
        }
        break;
    case "double":
        if (double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var doubleValue))
        {
            Console.WriteLine(doubleValue.ToString(CultureInfo.InvariantCulture));
            return 0;
        }
        break;
    case "bool":
        if (bool.TryParse(value, out var boolValue))
        {
            Console.WriteLine(boolValue.ToString().ToLowerInvariant());
            return 0;
        }
        break;
    case "string":
        Console.WriteLine(value);
        return 0;
}

Console.WriteLine($"Ungültige Umwandlung für Typ '{type}' mit Wert '{value}'.");
return 1;
