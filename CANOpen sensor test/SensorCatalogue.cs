using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace CANOpen_sensor_test;

public class SensorCatalogue
{
    public Dictionary<string, DeviceInfo> Devices { get; set; }
        = new Dictionary<string, DeviceInfo>();

    public static SensorCatalogue Load(string path)
    {
        try
        {
            string json = File.ReadAllText(path);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true   // <— this line
            };

            var cat = JsonSerializer.Deserialize<SensorCatalogue>(json, options);
            if (cat?.Devices is not null)
                return cat;
        }
        catch
        {
            // fallback to embedded…
        }

        // fallback: load embedded default
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("CANOpen_sensor_test.sensor-catalogue.json");
        if (stream is null)
            throw new FileNotFoundException("Built-in catalogue missing!");
        using var reader = new StreamReader(stream);
        string embeddedJson = reader.ReadToEnd();
        return JsonSerializer.Deserialize<SensorCatalogue>(embeddedJson)
               ?? throw new InvalidOperationException("Invalid embedded catalogue");
    }
}

public class DeviceInfo
{
    public string Description { get; set; } = "";
    public double Range_mm { get; set; }
    public int Total_steps { get; set; }
    public double Step_mm => Range_mm / Total_steps;
}
