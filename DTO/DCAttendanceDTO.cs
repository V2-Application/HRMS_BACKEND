using System;
using System.ComponentModel.DataAnnotations;

public class DCAttendanceDTO
{
   
        [Required]
        public string? Ecode { get; set; }
        [Required]
        public bool? Status { get; set; }
   
}