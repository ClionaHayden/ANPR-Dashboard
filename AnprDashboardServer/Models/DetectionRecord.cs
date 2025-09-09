public class DetectionRecord
{
    public int Id { get; set; }
    public string Plate { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string FilePath { get; set; } = string.Empty;
}
