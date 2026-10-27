using LibreHardwareMonitor.Hardware;
using System.Globalization;

// Папка, из которой запускается программа.
string projectFolder = Directory.GetCurrentDirectory();

string logPath = Path.Combine(projectFolder, "fan_log.txt");
string errorPath = Path.Combine(projectFolder, "fan_errors.txt");

// Номера датчиков для записи в лог.
var targets = new[]
{
    new FanTarget(1, "CPU Fan", false),
    new FanTarget(2, "System Fan #1", false),
    new FanTarget(3, "GPU Fan 1", true),
    new FanTarget(4, "GPU Fan 2", true)
};

var computer = new Computer
{
    IsMotherboardEnabled = true,
    IsGpuEnabled = true
};

try
{
    computer.Open();

    Console.WriteLine("Монитор вентиляторов запущен.");
    Console.WriteLine($"Файл лога: {logPath}");
    Console.WriteLine("Проверка каждые 5 минут.");
    Console.WriteLine("Для остановки нажми Ctrl + C.");
    Console.WriteLine();

    while (true)
    {
        try
        {
            var readings = new List<FanReading>();

            foreach (IHardware hardware in computer.Hardware)
            {
                CollectReadings(hardware, false, readings);
            }

            string timestamp = DateTime.Now.ToString(
                "yyyy-MM-dd-HH-mm",
                CultureInfo.InvariantCulture
            );

            foreach (FanTarget target in targets)
            {
                var matches = readings
                    .Where(r => r.Name == target.Name && r.IsGpu == target.IsGpu)
                    .ToList();

                if (matches.Count != 1 || matches[0].Rpm is null)
                {
                    string error =
                        $"{timestamp} Fan_{target.Number} ({target.Name}): " +
                        "показание недоступно или найдено несколько датчиков.";

                    File.AppendAllText(
                        errorPath,
                        error + Environment.NewLine
                    );

                    Console.WriteLine(error);
                    continue;
                }

                float rpm = matches[0].Rpm!.Value;
                string state = rpm > 0 ? "ON" : "OFF";

                string line =
                    $"{timestamp} Fan_{target.Number}_state: {state}";

                File.AppendAllText(
                    logPath,
                    line + Environment.NewLine
                );

                Console.WriteLine(
                    $"{line} ({target.Name}: {rpm:F0} RPM)"
                );
            }

            Console.WriteLine();
        }
        catch (Exception ex)
        {
            string error =
                $"{DateTime.Now:yyyy-MM-dd-HH-mm} " +
                $"Ошибка проверки: {ex.Message}";

            File.AppendAllText(
                errorPath,
                error + Environment.NewLine
            );

            Console.WriteLine(error);
        }

        Thread.Sleep(TimeSpan.FromMinutes(5));
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Не удалось запустить монитор: {ex.Message}");
}
finally
{
    computer.Close();
}

static void CollectReadings(
    IHardware hardware,
    bool parentIsGpu,
    List<FanReading> readings)
{
    bool isGpu =
        parentIsGpu ||
        hardware.HardwareType == HardwareType.GpuNvidia ||
        hardware.HardwareType == HardwareType.GpuAmd ||
        hardware.HardwareType == HardwareType.GpuIntel;

    hardware.Update();

    foreach (ISensor sensor in hardware.Sensors)
    {
        if (sensor.SensorType == SensorType.Fan)
        {
            readings.Add(
                new FanReading(sensor.Name, isGpu, sensor.Value)
            );
        }
    }

    foreach (IHardware subHardware in hardware.SubHardware)
    {
        CollectReadings(subHardware, isGpu, readings);
    }
}

record FanTarget(int Number, string Name, bool IsGpu);
record FanReading(string Name, bool IsGpu, float? Rpm);