using DocumentFormat.OpenXml.Drawing.Charts;
using HRMSAPI.DTO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SI = System.IO;
using Newtonsoft.Json;
using HRMSAPI.Services;

namespace HRMSAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]

    public class AutoUpdateController : ControllerBase
    {
        private readonly IPermissionNotificationService _notificationService;

        public AutoUpdateController(IPermissionNotificationService notificationService)
        {
            _notificationService = notificationService;
        }
        [HttpPost]
        [Route("POST")]
        public async Task<IActionResult> POST([FromBody] AutoupdateDto request)
        {
            Autoupdate autoUpdate = new Autoupdate();
            try
            {
                if (request.Version_Code != "" && request.Version_Code != null
                    && request.Version_Name != "" && request.Version_Name != null
                    && request.Drive_Link != "" && request.Drive_Link != null)
                {
                    var existingAutoupdates = new List<AutoupdateDto>();

                //var fileContent = SI.File.ReadAllText("D:\\V2Projects\\HRMS\\PublishedCode\\Backend_New\\AutoUpdate.txt");
                var fileContent = SI.File.ReadAllText("D:\\Applications\\HRMSApi\\AutoUpdate.txt");
                
                existingAutoupdates = JsonConvert.DeserializeObject<List<AutoupdateDto>>(fileContent);

                    var AutoupdateResponse = new AutoupdateDto
                    {
                        Project_Name = request.Project_Name,
                        Project_PlatForm = request.Project_PlatForm,
                        Version_Code = request.Version_Code,
                        Version_Name = request.Version_Name,
                        Drive_Link = request.Drive_Link,
                        API_URL = request.API_URL,
                    };

                    if (existingAutoupdates != null && existingAutoupdates.Any(c => c.Project_Name == request.Project_Name))
                    {
                        var proj = existingAutoupdates.First(c => c.Project_Name == request.Project_Name);
                        proj.Project_Name = request.Project_Name;
                        proj.Project_PlatForm = request.Project_PlatForm;
                        proj.Version_Code = request.Version_Code;
                        proj.Version_Name = request.Version_Name;
                        proj.Drive_Link = request.Drive_Link;
                        proj.API_URL = request.API_URL;
                    }
                    else
                        existingAutoupdates.Add(AutoupdateResponse);

                    fileContent = JsonConvert.SerializeObject(existingAutoupdates);

                    SI.File.WriteAllText("D:\\V2Projects\\HRMS\\PublishedCode\\Backend_New\\AutoUpdate.txt", fileContent);

                    autoUpdate.Data.Add(AutoupdateResponse);
                    autoUpdate.Status = true;
                    autoUpdate.Message = "";
                    return Ok(JsonConvert.SerializeObject(autoUpdate));
                }
                else
                {
                    autoUpdate.Status = false;
                    autoUpdate.Message = "All Field is Mandatory.";
                    return BadRequest(JsonConvert.SerializeObject(autoUpdate));
                }
            }
            catch (Exception ex)
            {
                autoUpdate.Status = false;
                autoUpdate.Message = "" + ex.Message + "";
                return BadRequest(JsonConvert.SerializeObject(autoUpdate));
            }
        }

        [HttpPost]
        [Route("GET")]
        public async Task<IActionResult> GET(string projName = "", string platform = "")
        {
            Autoupdate Autoupdate = new Autoupdate();

            try
            {
                var fileContent = SI.File.ReadAllText("D:\\V2Projects\\HRMS\\PublishedCode\\Backend_New\\AutoUpdate.txt");
                var existingAutoupdates = JsonConvert.DeserializeObject<List<AutoupdateDto>>(fileContent);

                //for HRMS
                if(string.IsNullOrEmpty(projName) && string.IsNullOrEmpty(platform))
                {
                    projName = "HRMS";
                    platform = "MOBILE";
                }

                var appRecord = existingAutoupdates.Where(c => c.Project_Name.ToUpper() == projName.ToUpper() 
                && c.Project_PlatForm.ToUpper() == platform.ToUpper()).FirstOrDefault();

                if (appRecord != null) 
                {
                    AutoupdateDto AutoupdateResponse = new AutoupdateDto();
                    AutoupdateResponse.Project_Name = appRecord.Project_Name;
                    AutoupdateResponse.Project_PlatForm = appRecord.Project_PlatForm;
                    AutoupdateResponse.Version_Code = appRecord.Version_Code;
                    AutoupdateResponse.Version_Name = appRecord.Version_Name;
                    AutoupdateResponse.Drive_Link = appRecord.Drive_Link;
                    AutoupdateResponse.API_URL = appRecord.API_URL;
                    Autoupdate.Data.Add(AutoupdateResponse);
                    Autoupdate.Status = true;
                    Autoupdate.Message = "";
                }

                //default
                ////string createText = "Hello and Welcome" + Environment.NewLine;
                ////File.WriteAllText(path, createText);
                //var readText = SI.File.ReadAllText("D:\\V2Projects\\HRMS\\PublishedCode\\Backend_New\\AutoUpdate.txt").Split(',');
               
                return Ok(JsonConvert.SerializeObject(Autoupdate));
            }
            catch (Exception ex)
            {
                Autoupdate.Status = false;
                Autoupdate.Message = "" + ex.Message + "";
                return BadRequest(JsonConvert.SerializeObject(Autoupdate));
            }
        }

        [HttpPost]
        [Route("NotifyVersionUpdate")]
        public async Task<IActionResult> NotifyVersionUpdate()
        {
            try
            {
                    // Notify all users about the version update
                    await _notificationService.NotifyAllUsersVersionUpdateAsync();

                    var response = new
                    {
                        Status = true,
                        Message = "Version update notification sent to all users successfully",
                    };

                    return Ok(JsonConvert.SerializeObject(response));
               
            }
            catch (Exception ex)
            {
                var errorResponse = new
                {
                    Status = false,
                    Message = $"Error sending version update notification: {ex.Message}"
                };
                return BadRequest(JsonConvert.SerializeObject(errorResponse));
            }
        }

        [HttpPost]
        [Route("LogoutUser")]
        public async Task<IActionResult> LogoutUser([FromQuery]string userId)
        {
            try
            {
                // Notify all users about the version update
                await _notificationService.LogoutUser(userId);

                var response = new
                {
                    Status = true,
                    Message = "Logout action sent successfully",
                };

                return Ok(JsonConvert.SerializeObject(response));

            }
            catch (Exception ex)
            {
                var errorResponse = new
                {
                    Status = false,
                    Message = $"Error sending version logout message: {ex.Message}"
                };
                return BadRequest(JsonConvert.SerializeObject(errorResponse));
            }
        }

        //[HttpPost]
        //[Route("NotifyVersionUpdateToGroup")]
        //public async Task<IActionResult> NotifyVersionUpdateToGroup([FromBody] AutoupdateDto request)
        //{
        //    try
        //    {
        //        if (request.Version_Code != "" && request.Version_Code != null
        //            && request.Version_Name != "" && request.Version_Name != null)
        //        {
        //            // Notify only users in the update group about the version update
        //            await _notificationService.NotifyVersionUpdateAsync(
        //                request.Version_Name, 
        //                request.Version_Code, 
        //                request.Drive_Link ?? ""
        //            );

        //            var response = new
        //            {
        //                Status = true,
        //                Message = "Version update notification sent to update group successfully",
        //                Data = new
        //                {
        //                    VersionName = request.Version_Name,
        //                    VersionCode = request.Version_Code,
        //                    DownloadLink = request.Drive_Link,
        //                    Timestamp = DateTime.UtcNow
        //                }
        //            };

        //            return Ok(JsonConvert.SerializeObject(response));
        //        }
        //        else
        //        {
        //            var errorResponse = new
        //            {
        //                Status = false,
        //                Message = "Version Name and Version Code are required fields."
        //            };
        //            return BadRequest(JsonConvert.SerializeObject(errorResponse));
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        var errorResponse = new
        //        {
        //            Status = false,
        //            Message = $"Error sending version update notification: {ex.Message}"
        //        };
        //        return BadRequest(JsonConvert.SerializeObject(errorResponse));
        //    }
        //}
    }
}