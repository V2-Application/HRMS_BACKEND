          
----Select * from ufn_GetEmpHolidayCounts_ForMonth('v46611','Jan-26')          
ALTER FUNCTION [dbo].[ufn_GetEmpHolidayCounts_ForMonth]          
(          
    @Ecode  nvarchar(50),          
    @Month  nvarchar(10), -- e.g. 'Aug-25' 
	@CycleStart date,
	@CycleEnd date

	
)          
RETURNS @R TABLE          
(          
    WeekdaysHolidayCount   int,          
    FinalSatHolidayCount   int          
)          
as          
begin           
--DECLARE @MonthYear  varchar(7) = 'Jan-26';    
    
DECLARE @FirstDate date =    
    TRY_CONVERT(date, '01 ' + REPLACE(UPPER(@Month), '-', ' '), 6);    
    
    
    
-- 26th of previous month to 25th of this month    
--DECLARE @CycleStart date = DATEADD(DAY, -6, @FirstDate); -- 26 prev month    
--DECLARE @CycleEnd   date = DATEADD(DAY, 24, @FirstDate); -- 25 same month    
    
--Declare @Month nvarchar(10) ='Aug-25';          
--Declare @Ecode nvarchar(50) = 'RTNR65';          
DECLARE @Date date =@CycleStart;-- @FirstDate;-- TRY_CONVERT(date, '01-' + @Month, 106);        
 Declare @MonthNumber int,@YearNo int;        
        
 SELECT @MonthNumber=MONTH(@Date),        
       @YearNo=YEAR(@Date);        
Declare @DOJ datetime;      
Declare @DOL datetime;      
Declare @Stcd nvarchar(10);          
Declare @LocationId int;          
Declare @IsFeteched bit=0;          
Declare @WeekdaysHolidayCount int =0;          
Declare @SatHolidayCount int =0; -- holiday sat count           
Declare @GroupId int =0;          
Declare @WeekOffSat int =0; -- budgeted weekof sat as per joining date          
Declare @TotalSat int =0; --total sat in month accordin to joiing date          
Declare @FinalSatHolidayCount int =0; -- final sat count           
          
--Fetch DOJ and LocationId OF EMployee          
Select @DOJ = cast(DOJ as datetime),@LocationId=LocationId,@DOL=cast(DateOfLeft as datetime) from tblEmployee (NOLOCK)          
where Ecode=@Ecode          
Select @Stcd=STCode          
from tblLocation (NOLOCK)          
where LocationId=@LocationId;          
        
--PRINt(@DOJ)          
--PRINt(@Stcd)         
          
IF @DOJ is NULL OR @LocationId is null          
begin          
 Set @WeekdaysHolidayCount =0;          
 Set @FinalSatHolidayCount=0;          
          
end          
else          
 begin          
 --First Priority :  LocationMapping          
 --weekDays Holidays          
 Select @WeekdaysHolidayCount = Count(*)          
 --Select *          
 from HolidayMaster          
 where DATENAME(WEEKDAY,HolidayDate) NOT IN ('Saturday', 'Sunday')          
 and LocationValue=@LocationId          
 and HolidayDate>=@DOJ        
   and ( @DOL IS NULL OR HolidayDate <= @DOL )       
   and HolidayDate between @CycleStart and @CycleEnd    
 --and MONTH(HolidayDate)=@MonthNumber and YEAR(HolidayDate)=@YearNo        
 and LocationType=1 and ISNULL(IsDeleted,0)=0 and ISNULL(IsActive,1)=1          
        
 --PRINT(@WeekdaysHolidayCount)        
 --sat Holidays          
 Select @SatHolidayCount = Count(*)          
 from HolidayMaster          
 where DATENAME(WEEKDAY,HolidayDate) = 'Saturday'          
 and LocationValue=@LocationId          
 and HolidayDate>=@DOJ         
  and ( @DOL IS NULL OR HolidayDate <= @DOL )      
     and HolidayDate between @CycleStart and @CycleEnd    
 --and MONTH(HolidayDate)=@MonthNumber and YEAR(HolidayDate)=@YearNo        
 and LocationType=1 and ISNULL(IsDeleted,0)=0 and ISNULL(IsActive,1)=1          
 --PRINT(@SatHolidayCount)        
          
 If @WeekdaysHolidayCount+@SatHolidayCount>0          
 begin          
  Set @IsFeteched=1          
 end          
  --PRINT(@IsFeteched)        
 If @IsFeteched=0          
 begin          
  --Second Priority : Group Mapping          
  Select @GroupId = GroupId from GroupWiseStoreCodeMapping          
  where ST_CD=@Stcd          
        
  --PRINT(@GroupId)        
  --weekDays Holidays          
  Select @WeekdaysHolidayCount = Count(*)          
  from HolidayMaster          
  where DATENAME(WEEKDAY,HolidayDate) NOT IN ('Saturday', 'Sunday')          
  and LocationValue=@GroupId          
  and HolidayDate>=@DOJ       
  and ( @DOL IS NULL OR HolidayDate <= @DOL )      
     and HolidayDate between @CycleStart and @CycleEnd    
  --and MONTH(HolidayDate)=@MonthNumber and YEAR(HolidayDate)=@YearNo        
  and LocationType=2 and ISNULL(IsDeleted,0)=0 and ISNULL(IsActive,1)=1          
        
  --PRINT(@WeekdaysHolidayCount)        
  --sat Holidays          
  Select @SatHolidayCount = Count(*)          
  from HolidayMaster          
  where DATENAME(WEEKDAY,HolidayDate) = 'Saturday'          
  and LocationValue=@GroupId          
  and HolidayDate>=@DOJ      
   and ( @DOL IS NULL OR HolidayDate <= @DOL )        
      and HolidayDate between @CycleStart and @CycleEnd    
  --and MONTH(HolidayDate)=@MonthNumber and YEAR(HolidayDate)=@YearNo        
  and LocationType=2 and ISNULL(IsDeleted,0)=0 and ISNULL(IsActive,1)=1          
 end          
 --Print('WeekDayHolidayCount : -')          
 --Print(@WeekdaysHolidayCount)          
 --Print('SatHolidayCount : -')          
 --Print(@SatHolidayCount)          
          
 SET @TotalSat= dbo.ufn_GetSaturdaysForMonth(@DOJ,@Month)          
 Select @WeekOffSat=AllowedSaturdays from dbo.fn_GetEmployeeWeekOffsByEcode(@Month, @Ecode);          
           
 --Print('TotalSat : -')          
 --Print(@TotalSat)          
 --Print('WeekOffSat : -')          
 --Print(@WeekOffSat)          
 --Print('FreeSatOtherThanWeekOFFS : -')          
 --Print( @TotalSat-@WeekOffSat)          
 IF @TotalSat-@WeekOffSat<@SatHolidayCount          
 begin          
  Set @FinalSatHolidayCount = @TotalSat-@WeekOffSat          
 end          
 else          
 begin          
  Set @FinalSatHolidayCount = @SatHolidayCount          
 end          
 --Print('FinalSatHolidayCount : -')          
 --Print( @FinalSatHolidayCount)          
end          
 INSERT INTO @R VALUES (@WeekdaysHolidayCount, @FinalSatHolidayCount);          
    RETURN;          
end 