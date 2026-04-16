using System;
using System.Collections.Generic;
using System.Data;
using HRMSAPI.DTO;
using HRMSAPI.Models.EvalutionForm;

public static class DataTableHelper
{
    public static DataTable ToFamilyDataTable(List<FamilyDto> familyList)
    {
        var table = new DataTable();

        table.Columns.Add("Name", typeof(string));
        table.Columns.Add("Relation", typeof(string));
        table.Columns.Add("Occupation", typeof(string));  // fixed typo here
        table.Columns.Add("Dependent", typeof(string));

        if (familyList == null || familyList.Count == 0) return table;

        foreach (var item in familyList)
        {
            table.Rows.Add(
                item.Name ?? string.Empty,
                item.Relation ?? string.Empty,
                item.Occupation ?? string.Empty,
                item.Dependent ?? string.Empty);
        }
        return table;
    }

    public static DataTable ToExperienceDataTable(List<ExperienceDto> experienceList)
    {
        var table = new DataTable();

        table.Columns.Add("TotalIndustryExperienceYears", typeof(int));
        table.Columns.Add("TotalRetailExperienceYears", typeof(int));
        table.Columns.Add("NoticePeriodDays", typeof(int));
        table.Columns.Add("CurrentCTC", typeof(decimal));
        table.Columns.Add("ExpectedCTC", typeof(decimal));

        if (experienceList == null || experienceList.Count == 0) return table;

        foreach (var item in experienceList)
        {
            table.Rows.Add(
                item.TotalIndustryExperienceYears,
                item.TotalRetailExperienceYears,
                item.NoticePeriodDays,
                item.CurrentCTC,
                item.ExpectedCTC);
        }
        return table;
    }

    public static DataTable ToKRAKPIDataTable(List<KRAKPIDto> list)
    {
        var table = new DataTable();

        table.Columns.Add("KRA", typeof(string));
        table.Columns.Add("KPI", typeof(string));

        if (list == null || list.Count == 0) return table;

        foreach (var item in list)
        {
            table.Rows.Add(
                item.KRA ?? string.Empty,
                item.KPI ?? string.Empty);
        }
        return table;
    }

    public static DataTable ToStrengthWeaknessDataTable(List<StrengthWeaknessDto> list)
    {
        var table = new DataTable();

        table.Columns.Add("Strength", typeof(string));
        table.Columns.Add("Weakness", typeof(string));
        table.Columns.Add("Biggestchallenges", typeof(string));


        if (list == null || list.Count == 0) return table;

        foreach (var item in list)
        {
            table.Rows.Add(
                item.Strength ?? string.Empty,
                item.Weakness ?? string.Empty,
                item.Biggestchallenges ?? string.Empty);
        }
        return table;
    }

    public static DataTable ToQuestionDataTable(List<QuestionDto> list)
    {
        var table = new DataTable();

        table.Columns.Add("Question", typeof(string));
        table.Columns.Add("Answer", typeof(string));

        if (list == null || list.Count == 0) return table;

        foreach (var item in list)
        {
            table.Rows.Add(
                item.Question ?? string.Empty,
                item.Answer ?? string.Empty);
        }
        return table;
    }

    public static DataTable ToReferenceDataTable(List<ReferenceDto> list)
    {
        var table = new DataTable();

        table.Columns.Add("FullName", typeof(string));
        table.Columns.Add("Company_Designation", typeof(string));
        table.Columns.Add("Contact_Details", typeof(string));
        table.Columns.Add("Bussiness_Details", typeof(string));

        if (list == null || list.Count == 0) return table;

        foreach (var item in list)
        {
            table.Rows.Add(
                item.FullName ?? string.Empty,
                item.Company_Designation ?? string.Empty,
                item.Contact_Details ?? string.Empty,
                item.Bussiness_Details ?? string.Empty
            );
        }

        return table;
    }

    public static DataTable ToApplicantDataTable(ApplicantDto applicant)
    {
        var table = new DataTable("Applicant");


        table.Columns.Add("ApplicantId", typeof(int));
        table.Columns.Add("IsApplicant", typeof(bool));
        table.Columns.Add("FullName", typeof(string));
        table.Columns.Add("DesignationId", typeof(int));
        table.Columns.Add("LocationId", typeof(int));


        var row = table.NewRow();
        row["ApplicantId"] = applicant.ApplicantId;
        row["IsApplicant"] = applicant.IsApplicant;
        row["FullName"] = applicant.FullName ?? string.Empty;
        row["DesignationId"] = applicant.DesignationId;
        row["LocationId"] = applicant.LocationId;

        table.Rows.Add(row);

        return table;
    }


  
        public static DataTable GetInterviewRoundsTable(List<InterviewRoundType> rounds)
        {
            var dt = new DataTable();
            dt.Columns.Add("TempId", typeof(int));
            dt.Columns.Add("Round", typeof(string));
            dt.Columns.Add("Level", typeof(string));
            dt.Columns.Add("Status", typeof(string));
            dt.Columns.Add("Remark", typeof(string));

            int tempId = 1;
            foreach (var round in rounds)
            {
                round.TempId = tempId; 
                dt.Rows.Add(round.TempId, round.Round, round.Level, round.Status, round.Remark);
                tempId++;
            }

            return dt;
        }

        public static DataTable GetInterviewersTable(List<InterviewRoundType> rounds)
        {
            var dt = new DataTable();
            dt.Columns.Add("InterviewRoundTempId", typeof(int));
            dt.Columns.Add("Name", typeof(string));
            dt.Columns.Add("Feedback", typeof(string));

            foreach (var round in rounds)
            {
                if (round.Interviewers != null)
                {
                    foreach (var interviewer in round.Interviewers)
                    {
                        dt.Rows.Add(round.TempId, interviewer.Name, interviewer.Feedback);
                    }
                }
            }

            return dt;
        }
    }



