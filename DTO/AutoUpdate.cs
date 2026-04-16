namespace HRMSAPI.DTO
{

    public class Autoupdate
    {
        public Boolean Status { get; set; }
        public string Message { get; set; }
        public List<AutoupdateDto> Data;
        public Autoupdate()
        {
            Data = new List<AutoupdateDto>();
        }
    }
    public class AutoupdateDto
    {
        public string Project_Name { get; set; }
        public string Project_PlatForm { get; set; }
        public string Version_Code { get; set; }
        public string Version_Name { get; set; }
        public string Drive_Link { get; set; }
        public string API_URL { get; set; }
    }
}
