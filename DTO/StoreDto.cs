using System;
using System.ComponentModel.DataAnnotations.Schema;

public class StoreDto
{
    public string Address { get; set; }
    public string Region { get; set; }
    public string Zone { get; set; }
    public string State { get; set; }
    public decimal Area { get; set; }
    public string StoreCode { get; set; }
    public DateTime LastUpdated { get; set; }
    public string StoreName { get; set; }
    public string OpeningMonth { get; set; }
    public string Cluster { get; set; }
    public int StoresId { get; set; }
    private string _openingDateString;

    public string OpeningDate
    {
        get => _openingDateString;
        set
        {
            _openingDateString = value;
            if (int.TryParse(value, out int excelDate))
            {
                ConvertedOpeningDate = DateTime.FromOADate(excelDate - 2);
            }
            else
            {
                ConvertedOpeningDate = DateTime.MinValue; // Default value if parsing fails
            }
        }
    }

    [NotMapped]
    public DateTime ConvertedOpeningDate { get; private set; }
}
