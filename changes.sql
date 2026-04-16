create table tblBonus_Upload
(
bigid int identity primary key,
E_Code nvarchar(20) not null,
Date datetime,
Acc_Number nvarchar(20) not null,
Amount decimal(18,2) not null,
UTR nvarchar(50) not null,
createdBy nvarchar(50),
createdOn datetime default getdate(),
updatedBy nvarchar(50),
updatedOn datetime,
isactive bit default 1,
isdeleted bit default 0
)

SELECT * FROM tblBonus_Upload


create table tblPF_Approvel
(
bigid int identity primary key,
E_Code nvarchar(20) not null,
_Month nvarchar(20) not null,
Challan_No nvarchar(50) not null,
Attechment nvarchar(100) not null,
createdBy nvarchar(50),
createdOn datetime default getdate(),
updatedBy nvarchar(50),
updatedOn datetime,
isactive bit default 1,
isdeleted bit default 0
)