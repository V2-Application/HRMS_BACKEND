using System.ComponentModel.DataAnnotations;

namespace HRMSAPI.DTO
{
    public class MinWageValidationRequestDto
    {
        public string STCode { get; set; } = string.Empty;

        public decimal Salary { get; set; }
    }

    public class MinWageValidationResponseDto
    {
        public bool IsSalaryAboveMinWage { get; set; }
        public decimal? MinWage { get; set; }
        public decimal Salary { get; set; }
        public string STCode { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class StateMinWageDto
    {
        public int Id { get; set; }
        public string StateName { get; set; } = string.Empty;
        public int MinWages { get; set; }
    }

    public class UpdateMinWageRequestDto
    {
        [Required(ErrorMessage = "Id is required")]
        public int Id { get; set; }

        [Required(ErrorMessage = "MinWages is required")]
        [Range(0, int.MaxValue, ErrorMessage = "MinWages must be greater than or equal to 0")]
        public int MinWages { get; set; }
    }
}

