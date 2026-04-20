namespace FinMind.Models;

public class IAOptions
{
    public bool Enabled { get; set; } = true;
    public string DatasetPath { get; set; } = "AI/Datasets/categoria_training_data.csv";
    public string ModelOutputPath { get; set; } = "AI/Models";
    public string ModelFileName { get; set; } = "categoria_model_desc_only_v2.zip";
    public decimal ConfidenceThreshold { get; set; } = 0.70m;
}
