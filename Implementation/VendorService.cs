using DocumentFormat.OpenXml.Bibliography;
using DocumentFormat.OpenXml.Office.Word;
using DocumentFormat.OpenXml.Office2010.Excel;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using Emgu.CV.Features2D;
using ExcelDataReader;
using HRMSAPI.Data;
using HRMSAPI.DTO;
using HRMSAPI.Interfaces;
using HRMSAPI.Models.Auth;
using MailKit.Search;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Org.BouncyCastle.Asn1.X509;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.Common;
using System.Diagnostics.Contracts;
using System.Net;
using System.Numerics;
using System.Reflection;
using System.Text;
using static HRMSAPI.Interfaces.IVendorService;
using static VendorEmployeeRequestDTO;

namespace HRMSAPI.Implementation
{
    public class VendorService : IVendorService
    {
        private readonly HRMSContext _context;
        public VendorService(HRMSContext context)
        {
            _context = context;
        }
        public async Task<Response> GetVendorListAsync(int pageNumber = 1, int pageSize = 10, DateTime? contractStartDate = null, DateTime? contractEndDate = null, string searchTerm = "")
        {
            var response = new Response();

            try
            {
                using (var connection = _context.Database.GetDbConnection())
                {
                    await connection.OpenAsync();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "GetVendorListWithEmployeeCount";
                        command.CommandType = CommandType.StoredProcedure;

                        // Input parameters
                        command.Parameters.Add(new SqlParameter("@PageNumber", pageNumber));
                        command.Parameters.Add(new SqlParameter("@PageSize", pageSize));
                        command.Parameters.Add(new SqlParameter("@ContractStartDate", contractStartDate ?? (object)DBNull.Value));
                        command.Parameters.Add(new SqlParameter("@ContractEndDate", contractEndDate ?? (object)DBNull.Value));
                        command.Parameters.Add(new SqlParameter("@SearchTerm", string.IsNullOrEmpty(searchTerm) ? "" : searchTerm));

                        // Output parameters
                        var totalVendorsParam = new SqlParameter("@TotalVendors", SqlDbType.Int) { Direction = ParameterDirection.Output };
                        command.Parameters.Add(totalVendorsParam);

                        var currentPageParam = new SqlParameter("@CurrentPageNumber", SqlDbType.Int) { Direction = ParameterDirection.Output };
                        command.Parameters.Add(currentPageParam);

                        // Execute reader
                        var vendorList = new List<VendorListDto>();
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                vendorList.Add(new VendorListDto
                                {
                                    VendorId = reader.GetInt64(reader.GetOrdinal("VendorId")),
                                    ContractorName = reader.IsDBNull(reader.GetOrdinal("ContractorName")) ? string.Empty : reader.GetString(reader.GetOrdinal("ContractorName")),
                                    ContractorCode = reader.IsDBNull(reader.GetOrdinal("ContractorCode")) ? string.Empty : reader.GetString(reader.GetOrdinal("ContractorCode")),
                                    ContractStartDate = reader.IsDBNull(reader.GetOrdinal("ContractStartDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("ContractStartDate")),
                                    ContractEndDate = reader.IsDBNull(reader.GetOrdinal("ContractEndDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("ContractEndDate")),
                                    EmployeeCount = reader.IsDBNull(reader.GetOrdinal("EmployeeCount")) ? 0 : reader.GetInt32(reader.GetOrdinal("EmployeeCount"))
                                });
                            }
                        }

                        // Read output parameters
                        int totalRecords = (int)(totalVendorsParam.Value ?? 0);
                        int currentPage = (int)(currentPageParam.Value ?? pageNumber);
                        int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

                        // Wrap data + paging in single object
                        var pagedResult = new PagedVendorListDto
                        {
                            Vendors = vendorList,
                            TotalRecords = totalRecords,
                            CurrentPage = currentPage,
                            PageSize = pageSize,
                            TotalPages = totalPages
                        };

                        // Assign to Response.Data
                        response.Status = vendorList.Count > 0;
                        response.StatusCode = vendorList.Count > 0 ? System.Net.HttpStatusCode.OK : System.Net.HttpStatusCode.NotFound;
                        response.Message = vendorList.Count > 0 ? "Data retrieved successfully" : "No data found";
                        response.Data = pagedResult;
                    }
                }
            }
            catch (Exception ex)
            {
                response.Status = false;
                response.StatusCode = System.Net.HttpStatusCode.InternalServerError;
                response.Message = ex.Message;
                response.Data = null;
            }

            return response;
        }
        public async Task<Response> GetVendorByIdAsync(long vendorId)
        {
            var response = new Response();

            try
            {
                using (var connection = _context.Database.GetDbConnection())
                {
                    await connection.OpenAsync();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "usp_GetVendorById";
                        command.CommandType = CommandType.StoredProcedure;

                        var param = command.CreateParameter();
                        param.ParameterName = "@VendorId";
                        param.Value = vendorId;
                        command.Parameters.Add(param);

                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (!reader.HasRows)
                            {
                                response.Status = false;
                                response.StatusCode = System.Net.HttpStatusCode.NotFound;
                                response.Message = "Vendor not found";
                                return response;
                            }

                            await reader.ReadAsync();
                            var vendor = new ResponseVendorDTO
                            {
                                VendorId = reader.GetInt64(reader.GetOrdinal("VendorId")),
                                ContractorName = reader.GetString(reader.GetOrdinal("ContractorName")),
                                ContractorCode = reader.GetString(reader.GetOrdinal("ContractorCode")),
                                ContractStartDate = reader.IsDBNull(reader.GetOrdinal("ContractStartDate")) ? null : reader.GetDateTime(reader.GetOrdinal("ContractStartDate")),
                                ContractEndDate = reader.IsDBNull(reader.GetOrdinal("ContractEndDate")) ? null : reader.GetDateTime(reader.GetOrdinal("ContractEndDate")),
                                ServiceCategoryDTO = reader.IsDBNull(reader.GetOrdinal("ServiceCategoryId")) ? null : new ResponseServiceDTO
                                {
                                    ServiceCategoryId = reader.GetInt32(reader.GetOrdinal("ServiceCategoryId")),
                                    ServiceName = reader.GetString(reader.GetOrdinal("ServiceCategoryName"))
                                },
                                ContractStatusesDTO = reader.IsDBNull(reader.GetOrdinal("ContarctStatusId")) ? null : new ContractStatusDTO
                                {
                                    ContarctStatusId = reader.GetInt32(reader.GetOrdinal("ContarctStatusId")),
                                    ContractStatus = reader.GetString(reader.GetOrdinal("ContractName"))
                                }
                            };

                            // Bank details
                            await reader.NextResultAsync();
                            var bankDetails = new List<ReponseBankDetailsDTO>();
                            while (await reader.ReadAsync())
                            {
                                bankDetails.Add(new ReponseBankDetailsDTO
                                {
                                    VendorBankId = reader.GetInt64(reader.GetOrdinal("VendorBankId")),
                                    BankName = reader.GetString(reader.GetOrdinal("BankName")),
                                    BranchName = reader.GetString(reader.GetOrdinal("BranchName")),
                                    AccountHolderName = reader.GetString(reader.GetOrdinal("AccountHolderName")),
                                    AccountNumber = reader.GetInt64(reader.GetOrdinal("AccountNumber")),
                                    IFSCCode = reader.GetString(reader.GetOrdinal("IFSCCode")),
                                    AccountType = reader.GetString(reader.GetOrdinal("AccountType")),
                                    PaymentMode = reader.GetString(reader.GetOrdinal("PaymentMode")),
                                    BeneficiaryName = reader.GetString(reader.GetOrdinal("BeneficiaryName")),
                                    GSTApplicability = reader.GetBoolean(reader.GetOrdinal("GSTApplicability")),
                                    BankVerificationStatus = reader.GetBoolean(reader.GetOrdinal("BankVerificationStatus")),
                                    VendorId = reader.GetInt64(reader.GetOrdinal("VendorId"))

                                });
                            }
                            vendor.VendorBankDetails = bankDetails;

                            // Compliance details
                            await reader.NextResultAsync();
                            var complianceDetails = new List<ReponseVendorComplianceDetailsDTO>();
                            while (await reader.ReadAsync())
                            {
                                complianceDetails.Add(new ReponseVendorComplianceDetailsDTO
                                {
                                    VendorComplianceId = reader.GetInt64(reader.GetOrdinal("VendorComplianceId")),
                                    ESICRegistrationNumber = reader.GetInt64(reader.GetOrdinal("ESICRegistrationNumber")),
                                    GSTIN = reader.GetString(reader.GetOrdinal("GSTIN")),
                                    LabourLicenseNumber = reader.GetString(reader.GetOrdinal("LabourLicenseNumber")),
                                    PFRegistrationNumber = reader.GetString(reader.GetOrdinal("PFRegistrationNumber")),
                                    PAN = reader.GetString(reader.GetOrdinal("PAN")),
                                    VendorId = reader.GetInt64(reader.GetOrdinal("VendorId"))
                                });
                            }
                            vendor.VendorComplianceDetails = complianceDetails;

                            // Contact details
                            await reader.NextResultAsync();
                            var contactDetails = new List<ReponseVendorContactDetailsDTO>();
                            while (await reader.ReadAsync())
                            {
                                contactDetails.Add(new ReponseVendorContactDetailsDTO
                                {
                                    VendorContactId = reader.GetInt64(reader.GetOrdinal("VendorContactId")),
                                    RegisteredAddress = reader.GetString(reader.GetOrdinal("RegisteredAddress")),
                                    SiteAddress = reader.GetString(reader.GetOrdinal("SiteAddress")),
                                    ContactPersonName = reader.GetString(reader.GetOrdinal("ContactPersonName")),
                                    MobileNumber = reader.GetInt64(reader.GetOrdinal("MobileNumber")),
                                    Email = reader.GetString(reader.GetOrdinal("Email")),
                                    VendorId = reader.GetInt64(reader.GetOrdinal("VendorId"))
                                });
                            }
                            vendor.VendorContactDetails = contactDetails;

                            response.Status = true;
                            response.StatusCode = System.Net.HttpStatusCode.OK;
                            response.Data = vendor;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                response.Status = false;
                response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                response.Message = ex.Message;
            }

            return response;
        }
        public async Task<Response> CreateVendor(CreateVendorDTO vendorDTO, long employeeId)
        {
            var response = new Response();

            try
            {
                using var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = "usp_CreateVendor";
                command.CommandType = CommandType.StoredProcedure;

                // Add parameters
                command.Parameters.Add(new SqlParameter("@ContractorName", vendorDTO.ContractorName));
                command.Parameters.Add(new SqlParameter("@ContractorCode", vendorDTO.ContractorCode));
                command.Parameters.Add(new SqlParameter("@ContractStartDate", vendorDTO.ContractStartDate));
                command.Parameters.Add(new SqlParameter("@ContractEndDate", vendorDTO.ContractEndDate));
                command.Parameters.Add(new SqlParameter("@ServiceCategoryId", vendorDTO.ServiceCategoryId));
                command.Parameters.Add(new SqlParameter("@ContarctStatusId", vendorDTO.ContarctStatusId));
                command.Parameters.Add(new SqlParameter("@CreatedBy", employeeId.ToString()));

                // TVPs
                var contactParam = new SqlParameter("@VendorContactDetails", SqlDbType.Structured)
                {
                    TypeName = "dbo.VendorContactType",
                    Value = vendorDTO.VendorContactDetails != null
                        ? vendorDTO.VendorContactDetails.ToDataTableContact()
                        : new DataTable()
                };
                command.Parameters.Add(contactParam);

                var bankParam = new SqlParameter("@VendorBankDetails", SqlDbType.Structured)
                {
                    TypeName = "dbo.VendorBankType",
                    Value = vendorDTO.VendorBankDetails != null
                        ? vendorDTO.VendorBankDetails.ToDataTableBank()
                        : new DataTable()
                };
                command.Parameters.Add(bankParam);

                var complianceParam = new SqlParameter("@VendorComplianceDetails", SqlDbType.Structured)
                {
                    TypeName = "dbo.VendorComplianceType",
                    Value = vendorDTO.VendorComplianceDetails != null
                        ? vendorDTO.VendorComplianceDetails.ToDataTableCompliance()
                        : new DataTable()
                };
                command.Parameters.Add(complianceParam);

                // Output parameters
                var vendorIdParam = new SqlParameter("@VendorId", SqlDbType.BigInt) { Direction = ParameterDirection.Output };
                command.Parameters.Add(vendorIdParam);

                var messageParam = new SqlParameter("@Message", SqlDbType.NVarChar, 500) { Direction = ParameterDirection.Output };
                command.Parameters.Add(messageParam);

                var statusParam = new SqlParameter("@Status", SqlDbType.Bit) { Direction = ParameterDirection.Output };
                command.Parameters.Add(statusParam);

                await command.ExecuteNonQueryAsync();

                response.Status = Convert.ToBoolean(statusParam.Value);
                response.Message = messageParam.Value.ToString();
                response.Data = vendorIdParam.Value != DBNull.Value ? vendorIdParam.Value : null;
                response.StatusCode = response.Status ? HttpStatusCode.OK : HttpStatusCode.BadRequest;
            }
            catch (Exception ex)
            {
                response.Status = false;
                response.Message = ex.Message;
                response.StatusCode = HttpStatusCode.BadRequest;
            }

            return response;
        }
        public async Task<Response> UpdateVendor(long vendorId, UpdateVendorDTO vendorDTO, long employeeId)
        {
            var response = new Response();
            using var connection = _context.Database.GetDbConnection();

            try
            {
                await connection.OpenAsync();
                using var command = connection.CreateCommand();
                command.CommandText = "usp_UpdateVendor";
                command.CommandType = CommandType.StoredProcedure;

                // Vendor Master
                command.Parameters.Add(new SqlParameter("@VendorId", vendorId));
                command.Parameters.Add(new SqlParameter("@ContractorName", vendorDTO.ContractorName ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@ContractorCode", vendorDTO.ContractorCode ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@ContractStartDate", vendorDTO.ContractStartDate ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@ContractEndDate", vendorDTO.ContractEndDate ?? (object)DBNull.Value));
                command.Parameters.Add(new SqlParameter("@ServiceCategoryId", vendorDTO.ServiceCategoryId));
                command.Parameters.Add(new SqlParameter("@ContarctStatusId", vendorDTO.ContarctStatusId));
                command.Parameters.Add(new SqlParameter("@UpdatedBy", employeeId.ToString()));

                // TVPs
                command.Parameters.Add(new SqlParameter("@VendorContactDetails", SqlDbType.Structured)
                {
                    TypeName = "dbo.VendorContactUpdateType",
                    Value = vendorDTO.VendorContactDetails != null ? ConvertToDataTableContact(vendorDTO.VendorContactDetails) : new DataTable()
                });

                command.Parameters.Add(new SqlParameter("@VendorBankDetails", SqlDbType.Structured)
                {
                    TypeName = "dbo.VendorBankUpdateType",
                    Value = vendorDTO.VendorBankDetails != null ? ConvertToDataTableBank(vendorDTO.VendorBankDetails) : new DataTable()
                });

                command.Parameters.Add(new SqlParameter("@VendorComplianceDetails", SqlDbType.Structured)
                {
                    TypeName = "dbo.VendorComplianceUpdateType",
                    Value = vendorDTO.VendorComplianceDetails != null ? ConvertToDataTableCompliance(vendorDTO.VendorComplianceDetails) : new DataTable()
                });

                // Output params
                var outputMessage = new SqlParameter("@Message", SqlDbType.NVarChar, 500) { Direction = ParameterDirection.Output };
                var outputStatus = new SqlParameter("@Status", SqlDbType.Int) { Direction = ParameterDirection.Output };
                command.Parameters.Add(outputMessage);
                command.Parameters.Add(outputStatus);

                await command.ExecuteNonQueryAsync();

                int status = Convert.ToInt32(outputStatus.Value);
                response = new Response
                {
                    Message = outputMessage.Value.ToString(),
                    Status = status == 1,
                    StatusCode = status switch
                    {
                        1 => System.Net.HttpStatusCode.OK,
                        404 => System.Net.HttpStatusCode.NotFound,
                        _ => System.Net.HttpStatusCode.BadRequest
                    }
                };
            }
            catch (Exception ex)
            {
                response = new Response
                {
                    Message = ex.Message,
                    Status = false,
                    StatusCode = System.Net.HttpStatusCode.InternalServerError
                };
            }
            finally
            {
                if (connection.State == ConnectionState.Open)
                    await connection.CloseAsync();
            }

            return response;
        }
        private DataTable ConvertToDataTableContact(List<UpdateVendorContactDetailsDTO> contacts)
        {
            var dt = new DataTable();
            dt.Columns.Add("VendorContactId", typeof(long));
            dt.Columns.Add("RegisteredAddress", typeof(string));
            dt.Columns.Add("SiteAddress", typeof(string));
            dt.Columns.Add("ContactPersonName", typeof(string));
            dt.Columns.Add("MobileNumber", typeof(long));
            dt.Columns.Add("Email", typeof(string));

            foreach (var c in contacts)
                dt.Rows.Add(c.VendorContactId, c.RegisteredAddress, c.SiteAddress, c.ContactPersonName, c.MobileNumber, c.Email);

            return dt;
        }

        private DataTable ConvertToDataTableBank(List<UpdateBankDetailsDTO> banks)
        {
            var dt = new DataTable();
            dt.Columns.Add("VendorBankId", typeof(long));
            dt.Columns.Add("BankName", typeof(string));
            dt.Columns.Add("BranchName", typeof(string));
            dt.Columns.Add("AccountHolderName", typeof(string));
            dt.Columns.Add("AccountNumber", typeof(long));
            dt.Columns.Add("IFSCCode", typeof(string));
            dt.Columns.Add("AccountType", typeof(string));
            dt.Columns.Add("PaymentMode", typeof(string));
            dt.Columns.Add("BeneficiaryName", typeof(string));
            dt.Columns.Add("GSTApplicability", typeof(bool));
            dt.Columns.Add("BankVerificationStatus", typeof(bool));

            foreach (var b in banks)
                dt.Rows.Add(b.VendorBankId, b.BankName, b.BranchName, b.AccountHolderName, b.AccountNumber, b.IFSCCode, b.AccountType, b.PaymentMode, b.BeneficiaryName, b.GSTApplicability, b.BankVerificationStatus);

            return dt;
        }

        private DataTable ConvertToDataTableCompliance(List<UpdateVendorComplianceDetailsDTO> compliances)
        {
            var dt = new DataTable();
            dt.Columns.Add("VendorComplianceId", typeof(long));
            dt.Columns.Add("PAN", typeof(string));
            dt.Columns.Add("GSTIN", typeof(string));
            dt.Columns.Add("PFRegistrationNumber", typeof(string));
            dt.Columns.Add("ESICRegistrationNumber", typeof(long));
            dt.Columns.Add("LabourLicenseNumber", typeof(string));

            foreach (var c in compliances)
                dt.Rows.Add(c.VendorComplianceId, c.PAN, c.GSTIN, c.PFRegistrationNumber, c.ESICRegistrationNumber, c.LabourLicenseNumber);

            return dt;
        }
        public async Task<Response> DeletevVendor(long id, long employeeId)
        {
            Response response = new Response();
            try
            {
                var vendor = await _context.tblVendorMasters.FindAsync(id);
                if (vendor == null)
                {
                    response = new Response()
                    {
                        Message = "Id is not exist",
                        Data = vendor,
                        Status = false,
                        StatusCode = System.Net.HttpStatusCode.NotFound
                    };
                }
                else
                {
                    vendor.IsActive = false;
                    vendor.DeletedOn = DateTime.Now;
                    vendor.DeletedBy = Convert.ToString(employeeId);
                    await _context.SaveChangesAsync();
                    response = new Response()
                    {
                        Message = "Deleted successfully",
                        Status = true,
                        StatusCode = System.Net.HttpStatusCode.OK
                    };
                }
            }
            catch (Exception ex)
            {

                response = new Response()
                {
                    Message = ex.Message,
                    Status = false,
                    StatusCode = System.Net.HttpStatusCode.BadRequest
                };
            }
            return response;
        }

        public async Task<Response> GetServiceCategory()
        {
            Response response = new Response();
            try
            {
                using (var connection = _context.Database.GetDbConnection())
                {
                    await connection.OpenAsync();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "usp_GetServiceCategory"; // Use procedure name only
                        command.CommandType = CommandType.StoredProcedure;


                        // Execute reader to get ServiceCategory list
                        var serviceCategory = new List<ResponseServiceDTO>();
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var service = new ResponseServiceDTO
                                {
                                    ServiceCategoryId = reader.GetInt32(reader.GetOrdinal("ServiceCategoryId")),
                                    ServiceName = reader.IsDBNull(reader.GetOrdinal("ServiceName")) ? string.Empty : reader.GetString(reader.GetOrdinal("ServiceName"))

                                };
                                serviceCategory.Add(service);
                            }
                        }

                        if (serviceCategory.Count > 0)
                        {
                            response = new Response
                            {
                                Status = true,
                                StatusCode = System.Net.HttpStatusCode.OK,
                                Data = serviceCategory,
                                Message = "Data get successfully"

                            };
                        }
                        else
                        {
                            response = new Response
                            {
                                Status = false,
                                StatusCode = System.Net.HttpStatusCode.NotFound,
                                Data = serviceCategory,
                                Message = "No Data"
                            };
                        }
                    }

                }

            }
            catch (Exception ex)
            {
                response = new Response
                {
                    Status = false,
                    StatusCode = System.Net.HttpStatusCode.InternalServerError,
                    Message = ex.Message
                };

            }
            return response;
        }

        public async Task<Response> GetNatureOfWork()
        {

            Response response = new Response();
            try
            {
                using (var connection = _context.Database.GetDbConnection())
                {
                    await connection.OpenAsync();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "usp_GetNatureOfWork"; // Use procedure name only
                        command.CommandType = CommandType.StoredProcedure;


                        // Execute reader to get Nature of work list
                        var natureOfWork = new List<NatureOfWorkDTO>();
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var workName = new NatureOfWorkDTO
                                {
                                    NatureOfWorkId = reader.GetInt32(reader.GetOrdinal("NatureOfWorkId")),
                                    WorkName = reader.IsDBNull(reader.GetOrdinal("WorkName")) ? string.Empty : reader.GetString(reader.GetOrdinal("WorkName"))

                                };
                                natureOfWork.Add(workName);
                            }
                        }

                        if (natureOfWork.Count > 0)
                        {
                            response = new Response
                            {
                                Status = true,
                                StatusCode = System.Net.HttpStatusCode.OK,
                                Data = natureOfWork,
                                Message = "Data get successfully"

                            };
                        }
                        else
                        {
                            response = new Response
                            {
                                Status = false,
                                StatusCode = System.Net.HttpStatusCode.NotFound,
                                Data = natureOfWork,
                                Message = "No Data"
                            };
                        }
                    }

                }

            }
            catch (Exception ex)
            {
                response = new Response
                {
                    Status = false,
                    StatusCode = System.Net.HttpStatusCode.InternalServerError,
                    Message = ex.Message
                };

            }
            return response;
        }

        public async Task<Response> GetContractStatus()
        {
            Response response = new Response();
            try
            {
                using (var connection = _context.Database.GetDbConnection())
                {
                    await connection.OpenAsync();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "usp_GetContractStatus"; // Use procedure name only
                        command.CommandType = CommandType.StoredProcedure;


                        // Execute reader to get ServiceCategory list
                        var contractStatus = new List<ContractStatusDTO>();
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var contarct = new ContractStatusDTO()
                                {
                                    ContarctStatusId = reader.GetInt32(reader.GetOrdinal("ContarctStatusId")),
                                    ContractStatus = reader.IsDBNull(reader.GetOrdinal("ContractName")) ? string.Empty : reader.GetString(reader.GetOrdinal("ContractName"))

                                };
                                contractStatus.Add(contarct);
                            }
                        }

                        if (contractStatus.Count > 0)
                        {
                            response = new Response
                            {
                                Status = true,
                                StatusCode = System.Net.HttpStatusCode.OK,
                                Data = contractStatus,
                                Message = "Data get successfully"

                            };
                        }
                        else
                        {
                            response = new Response
                            {
                                Status = false,
                                StatusCode = System.Net.HttpStatusCode.NotFound,
                                Data = contractStatus,
                                Message = "No Data"
                            };
                        }
                    }

                }

            }
            catch (Exception ex)
            {
                response = new Response
                {
                    Status = false,
                    StatusCode = System.Net.HttpStatusCode.InternalServerError,
                    Message = ex.Message
                };

            }
            return response;
        }

        public async Task<Response> CreateServiceCategory(RequestServiceDTO serviceDTO)
        {

            Response response = new Response();
            try
            {
                using (var connection = _context.Database.GetDbConnection())
                {
                    await connection.OpenAsync();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "usp_AddServiceCategory"; // Use procedure name only
                        command.CommandType = CommandType.StoredProcedure;
                        var param = new SqlParameter("@ServiceName", SqlDbType.NVarChar, 50);
                        param.Value = serviceDTO.ServiceName;
                        command.Parameters.Add(param);
                        var result = await command.ExecuteNonQueryAsync();

                        if (result == 1)
                        {
                            response = new Response
                            {
                                Status = true,
                                StatusCode = System.Net.HttpStatusCode.OK,
                                Message = "Inserted successfully"

                            };
                        }
                        else
                        {
                            response = new Response
                            {
                                Status = false,
                                StatusCode = System.Net.HttpStatusCode.BadRequest,
                                Message = "Some thing went wrong"
                            };

                        }

                    }

                }
            }
            catch (Exception ex)
            {

                response = new Response
                {
                    Status = false,
                    StatusCode = System.Net.HttpStatusCode.InternalServerError,
                    Message = ex.Message
                };

            }

            return response;
        }


        public async Task<Response> InsertVendorEmployee(VendorEmployeeRequestDTO request, string CreatedBy)
        {
            Response response = new Response();

            // DTO validation
            var context = new ValidationContext(request);
            var validationResults = new List<ValidationResult>();
            bool isValid = Validator.TryValidateObject(request, context, validationResults, true);

            if (!isValid)
            {
                return new Response
                {
                    Status = false,
                    StatusCode = System.Net.HttpStatusCode.BadRequest,
                    Message = string.Join("; ", validationResults.Select(r => r.ErrorMessage))
                };
            }
            try
            {
                await using var connection = new SqlConnection(_context.Database.GetConnectionString());
                if (connection.State != System.Data.ConnectionState.Open)
                {
                    await connection.OpenAsync();
                }

                //  Check duplicates
                var (panExists, aadhaarExists, emailExist, mobileExists) = await CheckDuplicatesAsync(request, connection);

                if (panExists) return new Response { Status = false, StatusCode = HttpStatusCode.Conflict, Message = "PAN already exists" };
                if (aadhaarExists) return new Response { Status = false, StatusCode = HttpStatusCode.Conflict, Message = "Aadhaar already exists" };
                if (emailExist) return new Response { Status = false, StatusCode = HttpStatusCode.Conflict, Message = "Email already exists" };
                if (mobileExists) return new Response { Status = false, StatusCode = HttpStatusCode.Conflict, Message = "Mobile number already exists" };

                //  Contract dates
                DateTime? contractStart = request.ContractStartDate?.Date;
                DateTime? contractEnd = request.ContractEndDate?.Date;

                string Password = "V2@123";
                string PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password);

                using var command = connection.CreateCommand();
                command.CommandText = "usp_InsertVendorEmployee";
                command.CommandType = CommandType.StoredProcedure;

                command.Parameters.Add(new SqlParameter("@ContractorCode", SqlDbType.NVarChar, 200) { Value = (object?)request.ContractorCode ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@FirstName", SqlDbType.NVarChar, 100) { Value = request.FirstName });
                command.Parameters.Add(new SqlParameter("@MiddleName", SqlDbType.NVarChar, 100) { Value = (object?)request.MiddleName ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@LastName", SqlDbType.NVarChar, 100) { Value = (object?)request.LastName ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@FATHER_S_NAME", SqlDbType.NVarChar, 50) { Value = (object?)request.FatherName ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@EMAIL_ADDRESS", SqlDbType.NVarChar, 100) { Value = request.Email });
                command.Parameters.Add(new SqlParameter("@MOBILE", SqlDbType.NVarChar, 20) { Value = request.Mobile });

                command.Parameters.Add(new SqlParameter("@DepartmentId", SqlDbType.Int) { Value = (object?)request.DepartmentId ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@DesignationId", SqlDbType.Int) { Value = (object?)request.DesignationId ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@LocationId", SqlDbType.Int) { Value = (object?)request.LocationId ?? DBNull.Value });

                command.Parameters.Add(new SqlParameter("@DOJ", SqlDbType.DateTime) { Value = (object?)request.DOJ ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@DOB", SqlDbType.Date) { Value = (object?)request.DOB ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@GENDER", SqlDbType.NVarChar, 10) { Value = (object?)request.Gender ?? DBNull.Value });

                command.Parameters.Add(new SqlParameter("@PasswordHash", SqlDbType.NVarChar, 255) { Value = PasswordHash });
                command.Parameters.Add(new SqlParameter("@Password", SqlDbType.NVarChar, 255) { Value = Password });

                command.Parameters.Add(new SqlParameter("@PAN_NO", SqlDbType.NVarChar, 50) { Value = request.PANNo });
                command.Parameters.Add(new SqlParameter("@AADHAR_NO", SqlDbType.NVarChar, 50) { Value = request.AadharNo });

                command.Parameters.Add(new SqlParameter("@PERMANENT_ADDRESS", SqlDbType.NVarChar, 255) { Value = (object?)request.PermanentAddress ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@PERMANENT_ADDRESS_PIN_CODE", SqlDbType.NVarChar, 10) { Value = (object?)request.PermanentAddressPinCode ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@CreatedOn", SqlDbType.DateTime) { Value = DateTime.Now });
                command.Parameters.Add(new SqlParameter("@CreatedBy", SqlDbType.NVarChar, 100) { Value = CreatedBy });

                command.Parameters.Add(new SqlParameter("@ContractStartDate", SqlDbType.Date) { Value = (object?)contractStart ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@ContractEndDate", SqlDbType.Date) { Value = (object?)contractEnd ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@HusbandName", SqlDbType.NVarChar, 100)
                {
                    Value = (object?)request.HusbandName ?? DBNull.Value
                });
                command.Parameters.Add(new SqlParameter("@Ecode", SqlDbType.NVarChar, 20)
                {
                    Value = (object?)request.Ecode ?? DBNull.Value
                });

                // Add salary parameters
                command.Parameters.Add(new SqlParameter("@BasicSalary", SqlDbType.Decimal) { Value = (object?)request.BasicSalary ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@CCA", SqlDbType.Decimal) { Value = (object?)request.CCA ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@DA", SqlDbType.Decimal) { Value = (object?)request.DA ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@ExtraAllowance", SqlDbType.Decimal) { Value = (object?)request.ExtraAllowance ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@SpecialAllowance", SqlDbType.Decimal) { Value = (object?)request.SpecialAllowance ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@HRA", SqlDbType.Decimal) { Value = (object?)request.HRA ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@GROSS_SALARY", SqlDbType.Decimal) { Value = (object?)request.GROSS_SALARY ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@monthlyGrossCTC", SqlDbType.Decimal) { Value = (object?)request.monthlyGrossCTC ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@annuallyNetCTC", SqlDbType.Decimal) { Value = (object?)request.annuallyNetCTC ?? DBNull.Value });

                var outputEcode = new SqlParameter("@NewEcode", SqlDbType.NVarChar, 50) { Direction = ParameterDirection.Output };
                command.Parameters.Add(outputEcode);

                await command.ExecuteNonQueryAsync();

                string newEcode = outputEcode.Value?.ToString();

                response = new Response
                {
                    Status = !string.IsNullOrEmpty(newEcode),
                    StatusCode = !string.IsNullOrEmpty(newEcode) ? System.Net.HttpStatusCode.OK : System.Net.HttpStatusCode.BadRequest,
                    Message = !string.IsNullOrEmpty(newEcode) ? "Inserted successfully" : "Insertion failed"
                };
            }
            catch (Exception ex)
            {
                response = new Response
                {
                    Status = false,
                    StatusCode = System.Net.HttpStatusCode.InternalServerError,
                    Message = ex.Message
                };
            }
            return response;
        }

        public async Task<Response> InsertVendorEmployee2(VendorEmployeeRequestDTO request, string CreatedBy, SqlConnection connection, SqlTransaction transaction)
        {
            Response response = new Response();

            // DTO validation
            var context = new ValidationContext(request);
            var validationResults = new List<ValidationResult>();
            bool isValid = Validator.TryValidateObject(request, context, validationResults, true);

            if (!isValid)
            {
                return new Response
                {
                    Status = false,
                    StatusCode = System.Net.HttpStatusCode.BadRequest,
                    Message = string.Join("; ", validationResults.Select(r => r.ErrorMessage))
                };
            }
            try
            {
                //  Check duplicates
                var (panExists, aadhaarExists, emailExist, mobileExists) = await CheckDuplicatesAsync2(request, connection, transaction);

                if (panExists) return new Response { Status = false, StatusCode = HttpStatusCode.Conflict, Message = "PAN already exists" };
                if (aadhaarExists) return new Response { Status = false, StatusCode = HttpStatusCode.Conflict, Message = "Aadhaar already exists" };
                if (emailExist) return new Response { Status = false, StatusCode = HttpStatusCode.Conflict, Message = "Email already exists" };
                if (mobileExists) return new Response { Status = false, StatusCode = HttpStatusCode.Conflict, Message = "Mobile number already exists" };

                //  Contract dates
                DateTime? contractStart = request.ContractStartDate?.Date;
                DateTime? contractEnd = request.ContractEndDate?.Date;

                string Password = "V2@123";
                string PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password);

                using var command = connection.CreateCommand();
                command.CommandText = "usp_InsertVendorEmployee2";
                command.CommandType = CommandType.StoredProcedure;
                command.Transaction = transaction;

                command.Parameters.Add(new SqlParameter("@ContractorCode", SqlDbType.NVarChar, 200) { Value = (object?)request.ContractorCode ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@FirstName", SqlDbType.NVarChar, 100) { Value = request.FirstName });
                command.Parameters.Add(new SqlParameter("@MiddleName", SqlDbType.NVarChar, 100) { Value = (object?)request.MiddleName ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@LastName", SqlDbType.NVarChar, 100) { Value = (object?)request.LastName ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@FATHER_S_NAME", SqlDbType.NVarChar, 50) { Value = (object?)request.FatherName ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@EMAIL_ADDRESS", SqlDbType.NVarChar, 100) { Value = request.Email });
                command.Parameters.Add(new SqlParameter("@MOBILE", SqlDbType.NVarChar, 20) { Value = request.Mobile });

                command.Parameters.Add(new SqlParameter("@DepartmentId", SqlDbType.Int) { Value = (object?)request.DepartmentId ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@DesignationId", SqlDbType.Int) { Value = (object?)request.DesignationId ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@LocationId", SqlDbType.Int) { Value = (object?)request.LocationId ?? DBNull.Value });

                command.Parameters.Add(new SqlParameter("@DOJ", SqlDbType.DateTime) { Value = (object?)request.DOJ ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@DOB", SqlDbType.Date) { Value = (object?)request.DOB ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@GENDER", SqlDbType.NVarChar, 10) { Value = (object?)request.Gender ?? DBNull.Value });

                command.Parameters.Add(new SqlParameter("@PasswordHash", SqlDbType.NVarChar, 255) { Value = PasswordHash });
                command.Parameters.Add(new SqlParameter("@Password", SqlDbType.NVarChar, 255) { Value = Password });

                command.Parameters.Add(new SqlParameter("@PAN_NO", SqlDbType.NVarChar, 50) { Value = request.PANNo });
                command.Parameters.Add(new SqlParameter("@AADHAR_NO", SqlDbType.NVarChar, 50) { Value = request.AadharNo });

                command.Parameters.Add(new SqlParameter("@PERMANENT_ADDRESS", SqlDbType.NVarChar, 255) { Value = (object?)request.PermanentAddress ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@PERMANENT_ADDRESS_PIN_CODE", SqlDbType.NVarChar, 10) { Value = (object?)request.PermanentAddressPinCode ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@CreatedOn", SqlDbType.DateTime) { Value = DateTime.Now });
                command.Parameters.Add(new SqlParameter("@CreatedBy", SqlDbType.NVarChar, 100) { Value = CreatedBy });

                command.Parameters.Add(new SqlParameter("@ContractStartDate", SqlDbType.Date) { Value = (object?)contractStart ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@ContractEndDate", SqlDbType.Date) { Value = (object?)contractEnd ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@HusbandName", SqlDbType.NVarChar, 100)
                {
                    Value = (object?)request.HusbandName ?? DBNull.Value
                });
                command.Parameters.Add(new SqlParameter("@Ecode", SqlDbType.NVarChar, 20)
                {
                    Value = (object?)request.Ecode ?? DBNull.Value
                });

                // Add salary parameters
                command.Parameters.Add(new SqlParameter("@BasicSalary", SqlDbType.Decimal) { Value = (object?)request.BasicSalary ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@CCA", SqlDbType.Decimal) { Value = (object?)request.CCA ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@DA", SqlDbType.Decimal) { Value = (object?)request.DA ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@ExtraAllowance", SqlDbType.Decimal) { Value = (object?)request.ExtraAllowance ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@SpecialAllowance", SqlDbType.Decimal) { Value = (object?)request.SpecialAllowance ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@HRA", SqlDbType.Decimal) { Value = (object?)request.HRA ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@GROSS_SALARY", SqlDbType.Decimal) { Value = (object?)request.GROSS_SALARY ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@monthlyGrossCTC", SqlDbType.Decimal) { Value = (object?)request.monthlyGrossCTC ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@annuallyNetCTC", SqlDbType.Decimal) { Value = (object?)request.annuallyNetCTC ?? DBNull.Value });

                var outputEcode = new SqlParameter("@NewEcode", SqlDbType.NVarChar, 50) { Direction = ParameterDirection.Output };
                command.Parameters.Add(outputEcode);

                await command.ExecuteNonQueryAsync();

                string newEcode = outputEcode.Value?.ToString();

                response = new Response
                {
                    Status = !string.IsNullOrEmpty(newEcode),
                    StatusCode = !string.IsNullOrEmpty(newEcode) ? System.Net.HttpStatusCode.OK : System.Net.HttpStatusCode.BadRequest,
                    Message = !string.IsNullOrEmpty(newEcode) ? "Inserted successfully" : "Insertion failed"
                };
            }
            catch (Exception ex)
            {
                response = new Response
                {
                    Status = false,
                    StatusCode = System.Net.HttpStatusCode.InternalServerError,
                    Message = ex.Message
                };
            }
            return response;
        }

        private async Task<(bool panExists, bool aadhaarExists, bool emailExists, bool mobileExists)>
  CheckDuplicatesAsync(VendorEmployeeRequestDTO request, DbConnection connection)
        {

            using var command = connection.CreateCommand();
            command.CommandText = @"
             SELECT
             SUM(CASE WHEN [PAN NO] = @PAN THEN 1 ELSE 0 END),
             SUM(CASE WHEN [AADHAR NO] = @AADHAR THEN 1 ELSE 0 END),
             SUM(CASE WHEN LOWER(LTRIM(RTRIM([EMAIL ADDRESS]))) = LOWER(@Email) THEN 1 ELSE 0 END),
             SUM(CASE WHEN [MOBILE] = @Mobile THEN 1 ELSE 0 END)
             FROM tblEmployee";

            command.Parameters.Add(new SqlParameter("@PAN", request.PANNo));
            command.Parameters.Add(new SqlParameter("@AADHAR", request.AadharNo));
            command.Parameters.Add(new SqlParameter("@Email", request.Email?.Trim().ToLower()));
            command.Parameters.Add(new SqlParameter("@Mobile", request.Mobile?.Trim()));

            using var reader = await command.ExecuteReaderAsync();
            await reader.ReadAsync();

            bool panExists = reader.GetInt32(0) > 0;
            bool aadhaarExists = reader.GetInt32(1) > 0;
            bool emailExists = !string.IsNullOrWhiteSpace(request.Email) && reader.GetInt32(2) > 0;
            bool mobileExists = !string.IsNullOrWhiteSpace(request.Mobile) && reader.GetInt32(3) > 0;

            return (panExists, aadhaarExists, emailExists, mobileExists);
        }

        private async Task<(bool panExists, bool aadhaarExists, bool emailExists, bool mobileExists)> CheckDuplicatesAsync2(VendorEmployeeRequestDTO request, DbConnection connection, SqlTransaction transaction)
        {

            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = @"
             SELECT
             SUM(CASE WHEN [PAN NO] = @PAN THEN 1 ELSE 0 END),
             SUM(CASE WHEN [AADHAR NO] = @AADHAR THEN 1 ELSE 0 END),
             SUM(CASE WHEN LOWER(LTRIM(RTRIM([EMAIL ADDRESS]))) = LOWER(@Email) THEN 1 ELSE 0 END),
             SUM(CASE WHEN [MOBILE] = @Mobile THEN 1 ELSE 0 END)
             FROM tblEmployee";

            command.Parameters.Add(new SqlParameter("@PAN", request.PANNo));
            command.Parameters.Add(new SqlParameter("@AADHAR", request.AadharNo));
            command.Parameters.Add(new SqlParameter("@Email", request.Email?.Trim().ToLower()));
            command.Parameters.Add(new SqlParameter("@Mobile", request.Mobile?.Trim()));

            using var reader = await command.ExecuteReaderAsync();
            await reader.ReadAsync();

            bool panExists = reader.GetInt32(0) > 0;
            bool aadhaarExists = reader.GetInt32(1) > 0;
            bool emailExists = !string.IsNullOrWhiteSpace(request.Email) && reader.GetInt32(2) > 0;
            bool mobileExists = !string.IsNullOrWhiteSpace(request.Mobile) && reader.GetInt32(3) > 0;

            return (panExists, aadhaarExists, emailExists, mobileExists);
        }


        public async Task<Response> UpdateVendorEmployeeAsync(string Ecode, string ContractorCode, UpdateVendorEmployeeRequestDTO request, string updateBy)
        {
            Response response = new Response();

            try
            {
                /// DTO validation
                var context = new ValidationContext(request);
                var validationResults = new List<ValidationResult>();
                bool isValid = Validator.TryValidateObject(request, context, validationResults, true);

                if (!isValid)
                {
                    // Collect error messages
                    response.Status = false;
                    response.StatusCode = System.Net.HttpStatusCode.BadRequest;
                    response.Message = string.Join("; ", validationResults.Select(v => v.ErrorMessage));
                    return response;
                }
                using var connection = _context.Database.GetDbConnection();
                if (connection.State != System.Data.ConnectionState.Open)
                {
                    await connection.OpenAsync();
                }
                //  Contract dates validation

                DateTime? contractStart = request.ContractStartDate?.Date;
                DateTime? contractEnd = request.ContractEndDate?.Date;

                if (contractStart.HasValue && contractEnd.HasValue && contractEnd < contractStart)
                {
                    return new Response
                    {
                        Status = false,
                        StatusCode = HttpStatusCode.BadRequest,
                        Message = "Contract End Date cannot be earlier than Contract Start Date."
                    };
                }

                //  Duplicate check

                var (panExists, aadhaarExists, emailExists, mobileExists) =
                    await CheckDuplicatesForUpdateAsync(request, Ecode, ContractorCode, connection);

                if (panExists) return new Response { Status = false, StatusCode = HttpStatusCode.Conflict, Message = "PAN already exists for another employee." };
                if (aadhaarExists) return new Response { Status = false, StatusCode = HttpStatusCode.Conflict, Message = "Aadhaar already exists for another employee." };
                if (emailExists) return new Response { Status = false, StatusCode = HttpStatusCode.Conflict, Message = "Email already exists for another employee." };
                if (mobileExists) return new Response { Status = false, StatusCode = HttpStatusCode.Conflict, Message = "Mobile number already exists for another employee." };

                using var command = connection.CreateCommand();
                command.CommandText = "usp_UpdateVendorEmployee";
                command.CommandType = CommandType.StoredProcedure;

                // Add all parameters (similar to your existing code)
                command.Parameters.Add(new SqlParameter("@Ecode", SqlDbType.VarChar, 20) { Value = Ecode });
                command.Parameters.Add(new SqlParameter("@ContractorCode", SqlDbType.NVarChar, 200) { Value = (object?)ContractorCode ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@FirstName", SqlDbType.NVarChar, 100) { Value = request.FirstName });
                command.Parameters.Add(new SqlParameter("@MiddleName", SqlDbType.NVarChar, 100) { Value = (object?)request.MiddleName ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@LastName", SqlDbType.NVarChar, 100) { Value = (object?)request.LastName ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@FATHER_S_NAME", SqlDbType.NVarChar, 50) { Value = (object?)request.FatherName ?? DBNull.Value });

                command.Parameters.Add(new SqlParameter("@EMAIL_ADDRESS", SqlDbType.NVarChar, 100) { Value = (object?)request.Email ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@MOBILE", SqlDbType.NVarChar, 20) { Value = (object?)request.Mobile ?? DBNull.Value });

                command.Parameters.Add(new SqlParameter("@DepartmentId", SqlDbType.Int) { Value = (object?)request.DepartmentId ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@DesignationId", SqlDbType.Int) { Value = (object?)request.DesignationId ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@LocationId", SqlDbType.Int) { Value = (object?)request.LocationId ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@DOJ", SqlDbType.DateTime) { Value = (object?)request.DOJ ?? DBNull.Value });

                command.Parameters.Add(new SqlParameter("@DOB", SqlDbType.Date) { Value = (object?)request.DOB ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@GENDER", SqlDbType.NVarChar, 10) { Value = (object?)request.Gender ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@PAN_NO", SqlDbType.NVarChar, 50) { Value = (object?)request.PANNo ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@AADHAR_NO", SqlDbType.NVarChar, 50) { Value = (object?)request.AadharNo ?? DBNull.Value });

                command.Parameters.Add(new SqlParameter("@PERMANENT_ADDRESS", SqlDbType.NVarChar, 255) { Value = (object?)request.PermanentAddress ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@PERMANENT_ADDRESS_PIN_CODE", SqlDbType.NVarChar, 10) { Value = (object?)request.PermanentAddressPinCode ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@UpdatedBy", SqlDbType.NVarChar, 100) { Value = updateBy });

                command.Parameters.Add(new SqlParameter("@IsActive", SqlDbType.Bit) { Value = request.IsActive ?? true });
                command.Parameters.Add(new SqlParameter("@HusbandName", SqlDbType.NVarChar, 100) { Value = (object?)request.HusbandName ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@ShiftId", SqlDbType.Int) { Value = (object?)request.ShiftId ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@ContractStartDate", SqlDbType.Date) { Value = (object?)contractStart ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@ContractEndDate", SqlDbType.Date) { Value = (object?)contractEnd ?? DBNull.Value });

                // Add salary parameters
                command.Parameters.Add(new SqlParameter("@BasicSalary", SqlDbType.Decimal) { Value = (object?)request.BasicSalary ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@CCA", SqlDbType.Decimal) { Value = (object?)request.CCA ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@DA", SqlDbType.Decimal) { Value = (object?)request.DA ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@ExtraAllowance", SqlDbType.Decimal) { Value = (object?)request.ExtraAllowance ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@SpecialAllowance", SqlDbType.Decimal) { Value = (object?)request.SpecialAllowance ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@HRA", SqlDbType.Decimal) { Value = (object?)request.HRA ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@GROSS_SALARY", SqlDbType.Decimal) { Value = (object?)request.GROSS_SALARY ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@monthlyGrossCTC", SqlDbType.Decimal) { Value = (object?)request.monthlyGrossCTC ?? DBNull.Value });
                command.Parameters.Add(new SqlParameter("@annuallyNetCTC", SqlDbType.Decimal) { Value = (object?)request.annuallyNetCTC ?? DBNull.Value });

                await command.ExecuteNonQueryAsync();

                response.Status = true;
                response.StatusCode = HttpStatusCode.OK;
                response.Message = "Vendor employee updated successfully";
            }
            catch (SqlException ex)
            {
                response.Status = false;
                response.StatusCode = ex.Message.Contains("Employee with given Ecode does not exist.")
                    ? HttpStatusCode.NotFound
                    : HttpStatusCode.BadRequest;
                response.Message = ex.Message;
            }
            catch (Exception ex)
            {
                response.Status = false;
                response.StatusCode = HttpStatusCode.InternalServerError;
                response.Message = ex.Message;
            }

            return response;
        }

        private async Task<(bool panExists, bool aadhaarExists, bool emailExists, bool mobileExists)>
CheckDuplicatesForUpdateAsync(UpdateVendorEmployeeRequestDTO request, string ecode, string contractorCode, DbConnection connection)
        {

            using var command = connection.CreateCommand();
            command.CommandText = @"
        SELECT
            SUM(CASE WHEN [PAN NO] = @PAN AND [Ecode] <> @Ecode AND [ContractorCode] <> @ContractorCode THEN 1 ELSE 0 END),
            SUM(CASE WHEN [AADHAR NO] = @AADHAR AND [Ecode] <> @Ecode AND [ContractorCode] <> @ContractorCode THEN 1 ELSE 0 END), 
            SUM(CASE WHEN [EMAIL ADDRESS] = @Email AND [Ecode] <> @Ecode AND [ContractorCode] <> @ContractorCode THEN 1 ELSE 0 END),
            SUM(CASE WHEN [MOBILE] = @Mobile AND [Ecode] <> @Ecode AND [ContractorCode] <> @ContractorCode THEN 1 ELSE 0 END)
        FROM tblEmployee";

            command.Parameters.Add(new SqlParameter("@PAN", (object?)request.PANNo ?? DBNull.Value));
            command.Parameters.Add(new SqlParameter("@AADHAR", (object?)request.AadharNo ?? DBNull.Value));
            command.Parameters.Add(new SqlParameter("@Email", (object?)request.Email ?? DBNull.Value));
            command.Parameters.Add(new SqlParameter("@Mobile", (object?)request.Mobile ?? DBNull.Value));
            command.Parameters.Add(new SqlParameter("@Ecode", ecode));
            command.Parameters.Add(new SqlParameter("@ContractorCode", contractorCode));

            using var reader = await command.ExecuteReaderAsync();
            await reader.ReadAsync();

            bool panExists = reader.GetInt32(0) > 0;
            bool aadhaarExists = reader.GetInt32(1) > 0;
            bool emailExists = !string.IsNullOrWhiteSpace(request.Email) && reader.GetInt32(2) > 0;
            bool mobileExists = !string.IsNullOrWhiteSpace(request.Mobile) && reader.GetInt32(3) > 0;

            return (panExists, aadhaarExists, emailExists, mobileExists);
        }


        public async Task<Response> GetVendorEmployeesListAsync(string contractorCode, string searchTerm = "", int? isActiveFilter = null, DateTime? contractStartDate = null, DateTime? contractEndDate = null, int pageNumber = 1, int pageSize = 10)
        {
            var response = new Response();

            try
            {
                using (var connection = _context.Database.GetDbConnection())
                {
                    await connection.OpenAsync();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "usp_GetVendorEmployeesListByFilter";
                        command.CommandType = CommandType.StoredProcedure;

                        // Input parameters
                        command.Parameters.Add(new SqlParameter("@ContractorCode", contractorCode));
                        command.Parameters.Add(new SqlParameter("@SearchTerm", string.IsNullOrEmpty(searchTerm) ? "" : searchTerm));
                        command.Parameters.Add(new SqlParameter("@IsActiveFilter", (object)isActiveFilter ?? DBNull.Value));
                        command.Parameters.Add(new SqlParameter("@ContractStartDate", (object)contractStartDate ?? DBNull.Value));
                        command.Parameters.Add(new SqlParameter("@ContractEndDate", (object)contractEndDate ?? DBNull.Value));
                        command.Parameters.Add(new SqlParameter("@PageNumber", pageNumber));
                        command.Parameters.Add(new SqlParameter("@PageSize", pageSize));

                        // Output parameter for total records
                        var totalRecordsParam = new SqlParameter("@TotalRecords", SqlDbType.Int)
                        {
                            Direction = ParameterDirection.Output
                        };
                        command.Parameters.Add(totalRecordsParam);

                        // Execute reader
                        var employeeList = new List<VendorEmployeeDTO>();
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                employeeList.Add(new VendorEmployeeDTO
                                {
                                    EmployeeId = reader.GetInt64(reader.GetOrdinal("EmployeeId")),
                                    Ecode = reader.IsDBNull(reader.GetOrdinal("Ecode")) ? string.Empty : reader.GetString(reader.GetOrdinal("Ecode")),
                                    FullName = reader.IsDBNull(reader.GetOrdinal("FullName")) ? string.Empty : reader.GetString(reader.GetOrdinal("FullName")),
                                    DOJ = reader.IsDBNull(reader.GetOrdinal("DOJ")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("DOJ")),
                                    IsActive = reader.IsDBNull(reader.GetOrdinal("IsActive")) ? false : reader.GetBoolean(reader.GetOrdinal("IsActive")),
                                    DepartmentName = reader.IsDBNull(reader.GetOrdinal("DepartmentName")) ? string.Empty : reader.GetString(reader.GetOrdinal("DepartmentName")),
                                    DesignationName = reader.IsDBNull(reader.GetOrdinal("DesignationName")) ? string.Empty : reader.GetString(reader.GetOrdinal("DesignationName")),
                                    //ContractorName = reader.IsDBNull(reader.GetOrdinal("ContractorName")) ? string.Empty : reader.GetString(reader.GetOrdinal("ContractorName")),
                                    ShiftName = reader.IsDBNull(reader.GetOrdinal("ShiftName")) ? string.Empty : reader.GetString(reader.GetOrdinal("ShiftName")),
                                    ContractStartDate = reader.IsDBNull(reader.GetOrdinal("ContractStartDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("ContractStartDate")),
                                    ContractEndDate = reader.IsDBNull(reader.GetOrdinal("ContractEndDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("ContractEndDate"))
                                });
                            }
                        }

                        // Read output parameter for total records
                        int totalRecords = (int)(totalRecordsParam.Value ?? 0);
                        int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

                        // Wrap in paged result DTO
                        var pagedResult = new PagedEmployeeListDto
                        {
                            Employees = employeeList,
                            TotalRecords = totalRecords,
                            CurrentPage = pageNumber,
                            PageSize = pageSize,
                            TotalPages = totalPages
                        };

                        // Assign to Response
                        response.Status = employeeList.Count > 0;
                        response.StatusCode = employeeList.Count > 0 ? System.Net.HttpStatusCode.OK : System.Net.HttpStatusCode.NotFound;
                        response.Message = employeeList.Count > 0 ? "Data retrieved successfully" : "No data found";
                        response.Data = pagedResult;
                    }
                }
            }
            catch (Exception ex)
            {
                response.Status = false;
                response.StatusCode = System.Net.HttpStatusCode.InternalServerError;
                response.Message = ex.Message;
                response.Data = null;
            }

            return response;
        }


        public async Task<Response> GetVendorEmployeesByIdAsync(string ecode, string contractorCode)
        {
            var response = new Response
            {
                Status = false,
                StatusCode = HttpStatusCode.OK,
                Data = null,
                Message = string.Empty
            };

            // Validate required parameters
            if (string.IsNullOrWhiteSpace(ecode) || string.IsNullOrWhiteSpace(contractorCode))
            {
                response.Message = "Ecode and ContractorCode are required.";
                return response;
            }

            try
            {
                using var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = "usp_GetVendorEmployeesByEcode";
                command.CommandType = CommandType.StoredProcedure;

                var ecodeParam = command.CreateParameter();
                ecodeParam.ParameterName = "@Ecode";
                ecodeParam.Value = ecode;
                command.Parameters.Add(ecodeParam);

                var contractorParam = command.CreateParameter();
                contractorParam.ParameterName = "@ContractorCode";
                contractorParam.Value = contractorCode;
                command.Parameters.Add(contractorParam);

                var employeeList = new List<VendorEmployeeResponseDTO>();

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    employeeList.Add(new VendorEmployeeResponseDTO
                    {
                        EmployeeId = reader["EmployeeId"] != DBNull.Value ? Convert.ToInt64(reader["EmployeeId"]) : 0,
                        Ecode = reader["Ecode"]?.ToString() ?? string.Empty,
                        FullName = reader["FULL NAME"]?.ToString() ?? string.Empty,
                        FirstName = reader["FirstName"]?.ToString() ?? string.Empty,
                        MiddleName = reader["MiddleName"]?.ToString() ?? string.Empty,
                        LastName = reader["LastName"]?.ToString() ?? string.Empty,
                        FatherName = reader["FatherName"]?.ToString() ?? string.Empty,
                        Email = reader["EMAIL ADDRESS"]?.ToString() ?? string.Empty,
                        Mobile = reader["MOBILE"]?.ToString() ?? string.Empty,
                        PresentAddress = reader["PRESENT ADDRESS"]?.ToString() ?? string.Empty,
                        PresentAddressPinCode = reader["PRESENT ADDRESS PIN CODE"]?.ToString() ?? string.Empty,
                        DOB = reader["DOB"] != DBNull.Value ? (DateTime?)reader["DOB"] : null,
                        Gender = reader["GENDER"]?.ToString() ?? string.Empty,
                        IsActive = reader["IsActive"] != DBNull.Value && (bool)reader["IsActive"],
                        UAN = reader["UAN NO"]?.ToString() ?? string.Empty,
                        PAN = reader["PAN NO"]?.ToString() ?? string.Empty,
                        Aadhar = reader["AADHAR NO"]?.ToString() ?? string.Empty,
                        PFApplicable = reader["PFApplicable"] != DBNull.Value && (bool)reader["PFApplicable"],
                        ESICApplicable = reader["ESICApplicable"] != DBNull.Value && (bool)reader["ESICApplicable"],
                        ShiftID = reader["ShiftID"] != DBNull.Value ? (int?)reader["ShiftID"] : null,
                        ShiftName = reader["ShiftName"]?.ToString() ?? string.Empty,
                        ESICNO = reader["ESICNO"]?.ToString() ?? string.Empty,
                        HusbandName = reader["Husband Name"]?.ToString() ?? string.Empty,
                        ContractorCode = reader["ContractorCode"]?.ToString() ?? string.Empty,
                        DepartmentId = reader["DepartmentId"] != DBNull.Value ? (int?)reader["DepartmentId"] : null,
                        DepartmentName = reader["DepartmentName"]?.ToString() ?? string.Empty,
                        DesignationId = reader["DesignationId"] != DBNull.Value ? (int?)reader["DesignationId"] : null,
                        DesignationName = reader["DesignationName"]?.ToString() ?? string.Empty,
                        ContractorName = reader["ContractorName"]?.ToString() ?? string.Empty,
                        LocationId = reader["LocationId"] != DBNull.Value ? (int?)reader["LocationId"] : null,
                        LocationName = reader["LocationName"]?.ToString() ?? string.Empty,
                        DOJ = reader.IsDBNull(reader.GetOrdinal("DOJ")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("DOJ")),
                        ContractStartDate = reader.IsDBNull(reader.GetOrdinal("ContractStartDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("ContractStartDate")),
                        ContractEndDate = reader.IsDBNull(reader.GetOrdinal("ContractEndDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("ContractEndDate")),
                        // Salary Fields
                        BasicSalary = reader["BasicSalary"] != DBNull.Value ? Convert.ToDecimal(reader["BasicSalary"]) : (decimal?)null,
                        CCA = reader["CCA"] != DBNull.Value ? Convert.ToDecimal(reader["CCA"]) : (decimal?)null,
                        DA = reader["DA"] != DBNull.Value ? Convert.ToDecimal(reader["DA"]) : (decimal?)null,
                        ExtraAllowance = reader["ExtraAllowance"] != DBNull.Value ? Convert.ToDecimal(reader["ExtraAllowance"]) : (decimal?)null,
                        SpecialAllowance = reader["SpecialAllowance"] != DBNull.Value ? Convert.ToDecimal(reader["SpecialAllowance"]) : (decimal?)null,
                        HRA = reader["HRA"] != DBNull.Value ? Convert.ToDecimal(reader["HRA"]) : (decimal?)null,
                        GROSS_SALARY = reader["GROSS SALARY"] != DBNull.Value ? Convert.ToDecimal(reader["GROSS SALARY"]) : (decimal?)null,
                        monthlyGrossCTC = reader["monthlyGrossCTC"] != DBNull.Value ? Convert.ToDecimal(reader["monthlyGrossCTC"]) : (decimal?)null,
                        annuallyNetCTC = reader["annuallyNetCTC"] != DBNull.Value ? Convert.ToDecimal(reader["annuallyNetCTC"]) : (decimal?)null

                    });
                }

                response.Status = true;
                response.Data = employeeList;
                response.Message = employeeList.Any()
                    ? "Employees retrieved successfully."
                    : "No employees found for given Ecode and ContractorCode.";
            }
            catch (Exception ex)
            {
                response.Status = false;
                response.Message = $"Error: {ex.Message}";
                response.Data = null;
                response.StatusCode = HttpStatusCode.InternalServerError;
            }

            return response;
        }

        //========== Get Contractor Code from  ContractorMasterDetails table  accordding New Implemenation 
        public async Task<Response> GetVendorEmployeesListAsync1(string contractorCode, string searchTerm = "", int? isActiveFilter = null, DateTime? contractStartDate = null, DateTime? contractEndDate = null, int pageNumber = 1, int pageSize = 10)
        {
            var response = new Response();

            try
            {
                using (var connection = _context.Database.GetDbConnection())
                {
                    await connection.OpenAsync();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "usp_GetVendorEmployeesListByFilter_back";
                        command.CommandType = CommandType.StoredProcedure;

                        // Input parameters
                        command.Parameters.Add(new SqlParameter("@ContractorCode", contractorCode));
                        command.Parameters.Add(new SqlParameter("@SearchTerm", string.IsNullOrEmpty(searchTerm) ? "" : searchTerm));
                        command.Parameters.Add(new SqlParameter("@IsActiveFilter", (object)isActiveFilter ?? DBNull.Value));
                        command.Parameters.Add(new SqlParameter("@ContractStartDate", (object)contractStartDate ?? DBNull.Value));
                        command.Parameters.Add(new SqlParameter("@ContractEndDate", (object)contractEndDate ?? DBNull.Value));
                        command.Parameters.Add(new SqlParameter("@PageNumber", pageNumber));
                        command.Parameters.Add(new SqlParameter("@PageSize", pageSize));

                        // Output parameter for total records
                        var totalRecordsParam = new SqlParameter("@TotalRecords", SqlDbType.Int)
                        {
                            Direction = ParameterDirection.Output
                        };
                        command.Parameters.Add(totalRecordsParam);

                        // Execute reader
                        var employeeList = new List<VendorEmployeeDTO>();
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                employeeList.Add(new VendorEmployeeDTO
                                {
                                    EmployeeId = reader.GetInt64(reader.GetOrdinal("EmployeeId")),
                                    Ecode = reader.IsDBNull(reader.GetOrdinal("Ecode")) ? string.Empty : reader.GetString(reader.GetOrdinal("Ecode")),
                                    FullName = reader.IsDBNull(reader.GetOrdinal("FullName")) ? string.Empty : reader.GetString(reader.GetOrdinal("FullName")),
                                    DOJ = reader.IsDBNull(reader.GetOrdinal("DOJ")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("DOJ")),
                                    IsActive = reader.IsDBNull(reader.GetOrdinal("IsActive")) ? false : reader.GetBoolean(reader.GetOrdinal("IsActive")),
                                    DepartmentName = reader.IsDBNull(reader.GetOrdinal("DepartmentName")) ? string.Empty : reader.GetString(reader.GetOrdinal("DepartmentName")),
                                    DesignationName = reader.IsDBNull(reader.GetOrdinal("DesignationName")) ? string.Empty : reader.GetString(reader.GetOrdinal("DesignationName")),
                                    //ContractorName = reader.IsDBNull(reader.GetOrdinal("ContractorName")) ? string.Empty : reader.GetString(reader.GetOrdinal("ContractorName")),
                                    ShiftName = reader.IsDBNull(reader.GetOrdinal("ShiftName")) ? string.Empty : reader.GetString(reader.GetOrdinal("ShiftName")),
                                    ContractStartDate = reader.IsDBNull(reader.GetOrdinal("ContractStartDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("ContractStartDate")),
                                    ContractEndDate = reader.IsDBNull(reader.GetOrdinal("ContractEndDate")) ? (DateTime?)null : reader.GetDateTime(reader.GetOrdinal("ContractEndDate"))
                                });
                            }
                        }

                        // Read output parameter for total records
                        int totalRecords = (int)(totalRecordsParam.Value ?? 0);
                        int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

                        // Wrap in paged result DTO
                        var pagedResult = new PagedEmployeeListDto
                        {
                            Employees = employeeList,
                            TotalRecords = totalRecords,
                            CurrentPage = pageNumber,
                            PageSize = pageSize,
                            TotalPages = totalPages
                        };

                        // Assign to Response
                        response.Status = employeeList.Count > 0;
                        response.StatusCode = employeeList.Count > 0 ? System.Net.HttpStatusCode.OK : System.Net.HttpStatusCode.NotFound;
                        response.Message = employeeList.Count > 0 ? "Data retrieved successfully" : "No data found";
                        response.Data = pagedResult;
                    }
                }
            }
            catch (Exception ex)
            {
                response.Status = false;
                response.StatusCode = System.Net.HttpStatusCode.InternalServerError;
                response.Message = ex.Message;
                response.Data = null;
            }

            return response;
        }
        public async Task<Response> GetContractorDetailsAsync(string contractorCode = null, string contractorName = null, string searchTerm = "", int pageNumber = 1, int pageSize = 10)
        {
            var response = new Response();

            try
            {
                using (var connection = _context.Database.GetDbConnection())
                {
                    await connection.OpenAsync();

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "usp_LoadContractorDetails";
                        command.CommandType = CommandType.StoredProcedure;

                        // Input parameters
                        command.Parameters.Add(new SqlParameter("@SearchTerm", string.IsNullOrEmpty(searchTerm) ? "" : searchTerm));
                        command.Parameters.Add(new SqlParameter("@ContractorCode", (object)contractorCode ?? DBNull.Value));
                        command.Parameters.Add(new SqlParameter("@ContractorName", (object)contractorName ?? DBNull.Value));
                        command.Parameters.Add(new SqlParameter("@PageNumber", pageNumber));
                        command.Parameters.Add(new SqlParameter("@PageSize", pageSize));

                        // Output parameter for total records
                        var totalRecordsParam = new SqlParameter("@TotalRecords", SqlDbType.Int)
                        {
                            Direction = ParameterDirection.Output
                        };
                        command.Parameters.Add(totalRecordsParam);

                        // Execute reader
                        var contractorList = new List<ContractorDTO>();
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                contractorList.Add(new ContractorDTO
                                {
                                    ContractorCode = reader.IsDBNull(reader.GetOrdinal("ContractorCode")) ? string.Empty : reader.GetString(reader.GetOrdinal("ContractorCode")),
                                    ContractorName = reader.IsDBNull(reader.GetOrdinal("ContractorName")) ? string.Empty : reader.GetString(reader.GetOrdinal("ContractorName")),
                                    ServiceCategory = reader.IsDBNull(reader.GetOrdinal("ServiceCategory")) ? string.Empty : reader.GetString(reader.GetOrdinal("ServiceCategory")),
                                    NatureOfWork = reader.IsDBNull(reader.GetOrdinal("NatureOfWork")) ? string.Empty : reader.GetString(reader.GetOrdinal("NatureOfWork")),
                                    RegisteredAddress = reader.IsDBNull(reader.GetOrdinal("RegisteredAddress")) ? string.Empty : reader.GetString(reader.GetOrdinal("RegisteredAddress")),
                                    SiteAddress = reader.IsDBNull(reader.GetOrdinal("SiteAddress")) ? string.Empty : reader.GetString(reader.GetOrdinal("SiteAddress")),
                                    PAN = reader.IsDBNull(reader.GetOrdinal("PAN")) ? string.Empty : reader.GetString(reader.GetOrdinal("PAN")),
                                    GSTIN = reader.IsDBNull(reader.GetOrdinal("GSTIN")) ? string.Empty : reader.GetString(reader.GetOrdinal("GSTIN")),
                                    EmployeeCount = reader.IsDBNull(reader.GetOrdinal("EmployeeCount")) ? 0 : reader.GetInt32(reader.GetOrdinal("EmployeeCount")),
                                    IsActive = reader.IsDBNull(reader.GetOrdinal("IsActive")) ? (bool?)null : reader.GetBoolean(reader.GetOrdinal("IsActive"))
                                });
                            }
                        }

                        // Read output parameter for total records
                        int totalRecords = (int)(totalRecordsParam.Value ?? 0);
                        int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

                        // Wrap in paged result DTO
                        var pagedResult = new PagedContractorListDto
                        {
                            Contractors = contractorList,
                            TotalRecords = totalRecords,
                            CurrentPage = pageNumber,
                            PageSize = pageSize,
                            TotalPages = totalPages
                        };

                        // Assign to Response
                        response.Status = contractorList.Count > 0;
                        response.StatusCode = contractorList.Count > 0 ? System.Net.HttpStatusCode.OK : System.Net.HttpStatusCode.NotFound;
                        response.Message = contractorList.Count > 0 ? "Data retrieved successfully" : "No data found";
                        response.Data = pagedResult;
                    }
                }
            }
            catch (Exception ex)
            {
                response.Status = false;
                response.StatusCode = System.Net.HttpStatusCode.InternalServerError;
                response.Message = ex.Message;
                response.Data = null;
            }

            return response;
        }

        public async Task<Response> GetContractorByCodeAsync(string contractorCode)
        {
            var response = new Response
            {
                Status = false,
                StatusCode = HttpStatusCode.OK,
                Data = null,
                Message = string.Empty
            };

            // Validate required parameter
            if (string.IsNullOrWhiteSpace(contractorCode))
            {
                response.Message = "ContractorCode is required.";
                return response;
            }

            try
            {
                using var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = "usp_GetContractorDetailsByContractorCode"; // Stored Procedure
                command.CommandType = CommandType.StoredProcedure;

                var contractorParam = command.CreateParameter();
                contractorParam.ParameterName = "@ContractorCode";
                contractorParam.Value = contractorCode;
                command.Parameters.Add(contractorParam);

                var contractorList = new List<ContractorResponseDTO>();

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    contractorList.Add(new ContractorResponseDTO
                    {
                        ContractId = reader["ContractId"] != DBNull.Value ? Convert.ToInt64(reader["ContractId"]) : 0,
                        ContractorName = reader["ContractorName"]?.ToString() ?? string.Empty,
                        ContractorCode = reader["ContractorCode"]?.ToString() ?? string.Empty,
                        ServiceCategory = reader["ServiceCategory"]?.ToString() ?? string.Empty,
                        NatureOfWork = reader["NatureOfWork"]?.ToString() ?? string.Empty,
                        ContractStartDate = reader["ContractStartDate"] != DBNull.Value ? (DateTime?)reader["ContractStartDate"] : null,
                        ContractEndDate = reader["ContractEndDate"] != DBNull.Value ? (DateTime?)reader["ContractEndDate"] : null,
                        ContractStatus = reader["ContractStatus"]?.ToString() ?? string.Empty,
                        RegisteredAddress = reader["RegisteredAddress"]?.ToString() ?? string.Empty,
                        SiteAddress = reader["SiteAddress"]?.ToString() ?? string.Empty,
                        ContactPersonName = reader["ContactPersonName"]?.ToString() ?? string.Empty,
                        MobileNumber = reader["MobileNumber"]?.ToString() ?? string.Empty,
                        EmailID = reader["EmailID"]?.ToString() ?? string.Empty,
                        PAN = reader["PAN"]?.ToString() ?? string.Empty,
                        GSTIN = reader["GSTIN"]?.ToString() ?? string.Empty,
                        PFRegistrationNumber = reader["PFRegistrationNumber"]?.ToString() ?? string.Empty,
                        ESICRegistrationNumber = reader["ESICRegistrationNumber"]?.ToString() ?? string.Empty,
                        LabourLicenseNumber = reader["LabourLicenseNumber"]?.ToString() ?? string.Empty,
                        BankName = reader["BankName"]?.ToString() ?? string.Empty,
                        BranchName = reader["BranchName"]?.ToString() ?? string.Empty,
                        AccountHolderName = reader["AccountHolderName"]?.ToString() ?? string.Empty,
                        AccountNumber = reader["AccountNumber"]?.ToString() ?? string.Empty,
                        IFSCCode = reader["IFSCCode"]?.ToString() ?? string.Empty,
                        AccountType = reader["AccountType"]?.ToString() ?? string.Empty,
                        PaymentMode = reader["PaymentMode"]?.ToString() ?? string.Empty,
                        BeneficiaryName = reader["BeneficiaryName"]?.ToString() ?? string.Empty,
                        GSTApplicability = reader["GSTApplicability"] != DBNull.Value && (bool)reader["GSTApplicability"],
                        BankVerificationStatus = reader["BankVerificationStatus"] != DBNull.Value && (bool)reader["BankVerificationStatus"]
                    });
                }

                response.Status = true;
                response.Data = contractorList;
                response.Message = contractorList.Any()
                    ? "Contractor details retrieved successfully."
                    : $"No contractor found for ContractorCode '{contractorCode}'.";
            }
            catch (Exception ex)
            {
                response.Status = false;
                response.Message = "An error occurred while fetching contractor details.";
                response.Data = null;
                response.StatusCode = HttpStatusCode.InternalServerError;
            }

            return response;
        }


        public async Task<Response> ImportVendorEmployeesBulk(IFormFile file, string createdBy, string contractorCode)
        {
            // Validate the uploaded file
            if (file == null || file.Length == 0)
                return new Response
                {
                    Status = false,
                    StatusCode = HttpStatusCode.BadRequest,
                    Message = "No file uploaded."
                };

            var allowedExtensions = new[] { ".xls", ".xlsx" };
            var ext = Path.GetExtension(file.FileName);
            if (!allowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                return new Response
                {
                    Status = false,
                    StatusCode = HttpStatusCode.BadRequest,
                    Message = "Invalid file type. Only Excel files are allowed."
                };

            // Read data from the Excel file into a list of VendorEmployeeRequestDTOBulk objects
            var employees = await ReadExcel(file, contractorCode);
            await using var connection = new SqlConnection(_context.Database.GetConnectionString());
            await connection.OpenAsync();

            using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

            try
            {
                foreach (var employee in employees)
                {
                    var response = await InsertVendorEmployee2(employee, createdBy, connection, transaction);
                    if (!response.Status)
                    {
                        await transaction.RollbackAsync();
                        return response;
                    }
                }

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                {
                    await transaction.RollbackAsync();
                    return new Response { Status = false, Message = ex.Message };
                }

            }

            return new Response { Status = true, Message = "Successfully Uploaded Employees." };
        }

        private static string? Clean(object? v)
        {
            var s = v?.ToString()?.Trim();
            return string.IsNullOrWhiteSpace(s) ? null : s;
        }

        private async Task<Dictionary<string, int>> GetLookupAsync(
    DbConnection connection,
    string tableName,
    string idColumn,
    string nameColumn,
    List<string> names)
        {
            if (names.Count == 0)
                return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            var parameters = names
                .Select((n, i) => $"@p{i}")
                .ToArray();

            var sql = $@"
        SELECT {idColumn}, {nameColumn}
        FROM {tableName}
        WHERE {nameColumn} IN ({string.Join(",", parameters)})";

            using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;

            for (int i = 0; i < names.Count; i++)
            {
                var p = cmd.CreateParameter();
                p.ParameterName = $"@p{i}";
                p.Value = names[i];
                cmd.Parameters.Add(p);
            }

            var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                dict[reader.GetString(1).Trim()] = reader.GetInt32(0);
            }

            return dict;
        }

        public async Task<List<VendorEmployeeRequestDTO>> ReadExcel(IFormFile file, string contractorCode)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            using var stream = file.OpenReadStream();
            using var reader = ExcelReaderFactory.CreateReader(stream);

            var result = reader.AsDataSet(new ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataTableConfiguration
                {
                    UseHeaderRow = true
                }
            });

            var table = result.Tables[0];

            var expectedHeaders = new[] { "Ecode", "FirstName", "MiddleName", "LastName", "Gender", "FatherName", "SpouseName", "DOB", "Mobile", "Email", "Address", "Pincode", "WorkLocation", "Department", "Designation", "DateOfJoining", "ContractStartDate", "ContractEndDate", "Shift", "Aadhar Number", "PAN", "BasicSalary", "CCA", "DA", "ExtraAllowance", "SpecialAllowance", "HRA", "GROSS_SALARY", "monthlyGrossCTC", "annuallyNetCTC" };

            if (table.Columns.Count != expectedHeaders.Length)
            {
                throw new Exception("Headers Mismatch.");
            }
            // Validate headers
            for (int i = 0; i < expectedHeaders.Length; i++)
            {
                var cellValue = table.Columns[i].ColumnName.Trim();
                if (!string.Equals(cellValue, expectedHeaders[i], System.StringComparison.OrdinalIgnoreCase))
                    throw new Exception($"Header mismatch at column {i + 1}: Expected '{expectedHeaders[i]}', found '{cellValue}'");
            }

            if (table.Rows.Count == 0)
            {
                throw new Exception("Excel is empty.");
            }

            var deptNames = table.AsEnumerable()
        .Select(r => Clean(r["Department"]))
        .Where(x => x != null)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

            var desgNames = table.AsEnumerable()
                .Select(r => Clean(r["Designation"]))
                .Where(x => x != null)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var locNames = table.AsEnumerable()
                .Select(r => Clean(r["WorkLocation"]))
                .Where(x => x != null)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var shiftNames = table.AsEnumerable()
                .Select(r => Clean(r["Shift"]))
                .Where(x => x != null)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();


            try
            {
                using var connection = _context.Database.GetDbConnection();
                await connection.OpenAsync();

                var deptMap = await GetLookupAsync(
               connection,
               "tblDepartment",
               "DepartmentId",
               "DepartmentName",
               deptNames);

                var desgMap = await GetLookupAsync(
                    connection,
                    "tblDesignation",
                    "DesignationId",
                    "DesignationName",
                    desgNames);

                var locMap = await GetLookupAsync(
                    connection,
                    "tblLocation",
                    "LocationId",
                    "STCode",
                    locNames);

                var shiftMap = await GetLookupAsync(
                    connection,
                    "tblShiftMaster",
                    "ShiftID",
                    "ShiftName",
                    shiftNames);


                foreach (DataRow row in table.Rows)
                {
                    // Check for required fields and ensure they're not null or empty
                    string firstName = row["FirstName"]?.ToString().Trim();
                    string workLocation = row["WorkLocation"]?.ToString().Trim();
                    string department = row["Department"]?.ToString().Trim();
                    string designation = row["Designation"]?.ToString().Trim();
                    string dateOfJoining = row["DateOfJoining"]?.ToString().Trim();
                    //string contractStartDate = row["ContractStartDate"]?.ToString().Trim();
                    //string contractEndDate = row["ContractEndDate"]?.ToString().Trim();
                    string shift = row["Shift"]?.ToString().Trim();
                    //string fatherName = row["FatherName"]?.ToString().Trim();
                    //string spouseName = row["SpouseName"]?.ToString().Trim();
                    string dateOfBirth = row["DOB"]?.ToString().Trim();
                    string gender = row["Gender"]?.ToString().Trim();
                    string email = row["Email"]?.ToString().Trim();
                    string mobile = row["Mobile"]?.ToString().Trim();
                    //string address = row["Address"]?.ToString().Trim();


                    // Check if required fields are empty or null
                    if (string.IsNullOrEmpty(firstName))
                        throw new Exception($"Missing required value 'FirstName' in Row {table.Rows.IndexOf(row) + 1}, Column 'FirstName'");

                    if (string.IsNullOrEmpty(workLocation))
                        throw new Exception($"Missing required value 'WorkLocation' in Row {table.Rows.IndexOf(row) + 1}, Column 'WorkLocation'");

                    if (string.IsNullOrEmpty(department))
                        throw new Exception($"Missing required value 'Department' in Row {table.Rows.IndexOf(row) + 1}, Column 'Department'");

                    if (string.IsNullOrEmpty(designation))
                        throw new Exception($"Missing required value 'Designation' in Row {table.Rows.IndexOf(row) + 1}, Column 'Designation'");

                    if (string.IsNullOrEmpty(dateOfJoining))
                        throw new Exception($"Missing required value 'DateOfJoining' in Row {table.Rows.IndexOf(row) + 1}, Column 'DateOfJoining'");

                    //if (string.IsNullOrEmpty(contractStartDate))
                    //    throw new Exception($"Missing required value 'ContractStartDate' in Row {table.Rows.IndexOf(row) + 1}, Column 'ContractStartDate'");

                    //if (string.IsNullOrEmpty(contractEndDate))
                    //    throw new Exception($"Missing required value 'ContractEndDate' in Row {table.Rows.IndexOf(row) + 1}, Column 'ContractEndDate'");

                    if (string.IsNullOrEmpty(shift))
                        throw new Exception($"Missing required value 'Shift' in Row {table.Rows.IndexOf(row) + 1}, Column 'Shift'");

                    //if (string.IsNullOrEmpty(fatherName) && string.IsNullOrEmpty(spouseName))
                    //    throw new Exception($"Missing required value either 'FatherName' or 'SpouseName' in Row {table.Rows.IndexOf(row) + 1}");

                    if (string.IsNullOrEmpty(dateOfBirth))
                        throw new Exception($"Missing required value 'DOB' in Row {table.Rows.IndexOf(row) + 1}, Column 'DOB'");

                    if (string.IsNullOrEmpty(gender))
                        throw new Exception($"Missing required value 'Gender' in Row {table.Rows.IndexOf(row) + 1}, Column 'Gender'");

                    if (string.IsNullOrEmpty(email))
                        throw new Exception($"Missing required value 'Email' in Row {table.Rows.IndexOf(row) + 1}, Column 'Email'");

                    if (string.IsNullOrEmpty(mobile))
                        throw new Exception($"Missing required value 'Mobile' in Row {table.Rows.IndexOf(row) + 1}, Column 'Mobile'");

                //    if (string.IsNullOrEmpty(address))
                //        throw new Exception($"Missing required value 'Address' in Row {table.Rows.IndexOf(row) + 1}, Column 'Address'");
                }


                // 3) Map rows -> DTO (and resolve IDs via dictionaries)
                var list = new List<VendorEmployeeRequestDTO>();

                foreach (DataRow row in table.Rows)
                {

                    var deptName = Clean(row["Department"]);
                    var desgName = Clean(row["Designation"]);
                    var locName = Clean(row["WorkLocation"]);
                    var shiftName = Clean(row["Shift"]);

                    // Decide how you want to handle unknown names:
                    // Option A: throw with row info
                    if (deptName != null && !deptMap.ContainsKey(deptName))
                        throw new Exception($"Unknown Department '{deptName}'");
                    if (desgName != null && !desgMap.ContainsKey(desgName))
                        throw new Exception($"Unknown Designation '{desgName}'");
                    if (locName != null && !locMap.ContainsKey(locName))
                        throw new Exception($"Unknown Location '{locName}'");
                    //if (shiftName != null && !shiftMap.ContainsKey(shiftName))
                    //    throw new Exception($"Unknown Shift '{shiftName}'");



                    var emp = new VendorEmployeeRequestDTO
                    {
                        ContractorCode = contractorCode,
                        Ecode = row["Ecode"]?.ToString().Trim(),
                        FirstName = row["FirstName"]?.ToString().Trim(),
                        MiddleName = row["MiddleName"]?.ToString().Trim(),
                        LastName = row["LastName"]?.ToString().Trim(),
                        FatherName = row["FatherName"]?.ToString().Trim(),
                        Email = row["Email"]?.ToString().Trim(),
                        Mobile = row["Mobile"]?.ToString().Trim(),
                        Gender = row["Gender"]?.ToString().Trim(),
                        PANNo = row["PAN"]?.ToString().Trim(),
                        AadharNo = row["Aadhar Number"]?.ToString().Trim(),
                        DepartmentId = deptName != null ? deptMap[deptName] : null,
                        DesignationId = desgName != null ? desgMap[desgName] : null,
                        LocationId = locName != null ? locMap[locName] : null,
                        ShiftId = 1,
                        DOJ = row["DateOfJoining"] != DBNull.Value ? Convert.ToDateTime(row["DateOfJoining"]) : (DateTime?)null,
                        DOB = row["DOB"] != DBNull.Value ? Convert.ToDateTime(row["DOB"]) : (DateTime?)null,
                        PermanentAddress = row["Address"]?.ToString().Trim(),
                        PermanentAddressPinCode = row["Pincode"]?.ToString().Trim(),
                        ContractStartDate = row["ContractStartDate"] != DBNull.Value ? Convert.ToDateTime(row["ContractStartDate"]) : (DateTime?)null,
                        ContractEndDate = row["ContractEndDate"] != DBNull.Value ? Convert.ToDateTime(row["ContractEndDate"]) : (DateTime?)null,
                        HusbandName = row["SpouseName"]?.ToString().Trim(),
                        // Salary fields
                        BasicSalary = row["BasicSalary"] != DBNull.Value ? Convert.ToDecimal(row["BasicSalary"]) : (decimal?)null,
                        CCA = row["CCA"] != DBNull.Value ? Convert.ToDecimal(row["CCA"]) : (decimal?)null,
                        DA = row["DA"] != DBNull.Value ? Convert.ToDecimal(row["DA"]) : (decimal?)null,
                        ExtraAllowance = row["ExtraAllowance"] != DBNull.Value ? Convert.ToDecimal(row["ExtraAllowance"]) : (decimal?)null,
                        SpecialAllowance = row["SpecialAllowance"] != DBNull.Value ? Convert.ToDecimal(row["SpecialAllowance"]) : (decimal?)null,
                        HRA = row["HRA"] != DBNull.Value ? Convert.ToDecimal(row["HRA"]) : (decimal?)null,
                        GROSS_SALARY = row["GROSS_SALARY"] != DBNull.Value ? Convert.ToDecimal(row["GROSS_SALARY"]) : (decimal?)null,
                        monthlyGrossCTC = row["monthlyGrossCTC"] != DBNull.Value ? Convert.ToDecimal(row["monthlyGrossCTC"]) : (decimal?)null,
                        annuallyNetCTC = row["annuallyNetCTC"] != DBNull.Value ? Convert.ToDecimal(row["annuallyNetCTC"]) : (decimal?)null
                    };

                    list.Add(emp);
                }
                connection.Close();
                return list;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        public async Task GetIdFromName(List<VendorEmployeeRequestDTOBulk> employees)
        {
            // Load all mappings once from DB

            var departmentDict = await _context.tblDepartments
                .GroupBy(d => d.DepartmentName.Trim().ToLower())
                .Select(g => g.FirstOrDefault()) // Pick the first matching record in case of duplicates
                .ToDictionaryAsync(d => d.DepartmentName.Trim().ToLower(), d => d.DepartmentId);


            var designationDict = await _context.tblDesignations
                .GroupBy(d => d.DesignationName.Trim().ToLower())
                .Select(g => g.FirstOrDefault()) // Pick the first matching record in case of duplicates
                .ToDictionaryAsync(d => d.DesignationName.Trim().ToLower(), d => d.DesignationId);

            var locationDict = await _context.tblLocations
                .GroupBy(l => l.LocationName.Trim().ToLower())
                .Select(g => g.FirstOrDefault())
                .ToDictionaryAsync(l => l.LocationName.Trim().ToLower(), l => l.LocationId);

            var shiftDict = await _context.tblShiftMasters
                .GroupBy(s => s.ShiftName.Trim().ToLower())
                .Select(g => g.FirstOrDefault())
                .ToDictionaryAsync(s => s.ShiftName.Trim().ToLower(), s => s.ShiftID);

            foreach (var emp in employees)
            {
                departmentDict.TryGetValue(emp.DepartmentName?.ToLower(), out var deptId);
                emp.DepartmentId = deptId;

                designationDict.TryGetValue(emp.DesignationName?.ToLower(), out var desigId);
                emp.DesignationId = desigId;

                locationDict.TryGetValue(emp.LocationName?.ToLower(), out var locId);
                emp.LocationId = locId;

                shiftDict.TryGetValue(emp.ShiftName?.ToLower(), out var shiftId);
                emp.ShiftId = shiftId;
            }
        }

        public async Task<List<string>> ValidateEmployees(List<VendorEmployeeRequestDTOBulk> employees)
        {
            var errors = new List<string>();
            int row = 2; // Excel rows start at 2 (assuming row 1 is the header)

            // Assuming existingContractorCodes is already loaded in the context
            var existingContractorCodes = await _context.tblVendorMasters
                .Select(v => v.ContractorCode.Trim().ToLower())
                .ToHashSetAsync();  // Fast lookup with HashSet

            foreach (var emp in employees)
            {
                // Check for ContractorCode existence and format
                if (string.IsNullOrWhiteSpace(emp.ContractorCode) ||
                    !existingContractorCodes.Contains(emp.ContractorCode.Trim().ToLower()))
                {
                    errors.Add($"Row {row}: ContractorCode '{emp.ContractorCode}' does not exist in the database.");
                }

                // Other validation checks for required fields like email, department, etc.
                var context = new ValidationContext(emp);
                var results = new List<ValidationResult>();

                // Validate required fields using data annotations
                if (!Validator.TryValidateObject(emp, context, results, true))
                {
                    errors.Add($"Row {row}: {string.Join(", ", results.Select(r => r.ErrorMessage))}");
                }
                // Additional checks for department, designation, etc.
                if (emp.DepartmentId == null)
                    errors.Add($"Row {row}: Department '{emp.DepartmentName}' not found.");
                if (emp.DesignationId == null)
                    errors.Add($"Row {row}: Designation '{emp.DesignationName}' not found.");
                if (emp.LocationId == null)
                    errors.Add($"Row {row}: Location '{emp.LocationName}' not found.");
                if (emp.ShiftId == null)
                    errors.Add($"Row {row}: Shift '{emp.ShiftName}' not found.");

                row++;
            }

            return errors;
        }



        public DataTable ToDataTable(List<VendorEmployeeRequestDTOBulk> list)
        {
            var table = new DataTable();
            table.Columns.AddRange(new[]
            {
                new DataColumn("ContractorCode"),
                new DataColumn("FirstName"),
                new DataColumn("MiddleName"),
                new DataColumn("LastName"),
                new DataColumn("FatherName"),
                new DataColumn("Email"),
                new DataColumn("Mobile"),
                new DataColumn("DepartmentId", typeof(int)),
                new DataColumn("DesignationId", typeof(int)),
                new DataColumn("LocationId", typeof(int)),
                new DataColumn("DOJ", typeof(DateTime)),
                new DataColumn("DOB", typeof(DateTime)),
                new DataColumn("Gender"),
                new DataColumn("PANNo"),
                new DataColumn("AadharNo"),
                new DataColumn("PermanentAddress"),
                new DataColumn("PermanentAddressPinCode"),
                new DataColumn("ShiftId", typeof(int)),
                new DataColumn("ContractStartDate", typeof(DateTime)),
                new DataColumn("ContractEndDate", typeof(DateTime)),
                new DataColumn("HusbandName"),
            });

            foreach (var e in list)
            {
                table.Rows.Add(
                    e.ContractorCode,
                    e.FirstName,
                    e.MiddleName,
                    e.LastName,
                    e.FatherName,
                    e.Email,
                    e.Mobile,
                    e.DepartmentId,
                    e.DesignationId,
                    e.LocationId,
                    e.DOJ,
                    e.DOB,
                    e.Gender,
                    e.PANNo,
                    e.AadharNo,
                    e.PermanentAddress,
                    e.PermanentAddressPinCode,
                    e.ShiftId,
                    e.ContractStartDate,
                    e.ContractEndDate,
                    e.HusbandName
                );
            }

            return table;

        }

    }
}

