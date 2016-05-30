DECLARE @ProjectSrc varchar(255)=(SELECT Project_id FROM tbl_projects where project_name='@projectSrc')
DECLARE @ProjectDst varchar(255)=(SELECT Project_id FROM tbl_projects where project_name='@ProjectDst')
DECLARE @project varchar(255)='@Area'
DECLARE @Area table (SrcPath varbinary(255), DstPath varbinary(255))
DECLARE @Iter table (SrcPath varbinary(255), DstPath varbinary(255))
INSERT @Area
SELECT SrcPath=Src.path, DstPath=Dst.Path FROM tbl_ClassificationNodePath Src JOIN tbl_ClassificationNodePath Dst on Src.AreaPath=Dst.AreaPath where Src.areaLevel1='Area' AND Src.AreaLevel2=@project and Src.teamProject=@projectSrc and Dst.teamProject=@projectDst
INSERT @Iter
SELECT SrcPath=Src.path, DstPath=Dst.Path FROM tbl_ClassificationNodePath Src JOIN tbl_ClassificationNodePath Dst on Src.AreaPath=Dst.AreaPath where Src.areaLevel1='Iteration' AND Src.AreaLevel2=@project and Src.teamProject=@projectSrc and Dst.teamProject=@projectDst


UPDATE  WITL SET 
	AreaPath=A.DstPath, 
	IterationPath=I.DstPath
	FROM tbl_WorkItemCoreWERE WITL
	join @area A ON WITL.AreaPath=A.SrcPath
	join @Iter I ON WITL.IterationPath=I.SrcPath
	
UPDATE  WITL SET 
	AreaPath=A.DstPath, 
	IterationPath=I.DstPath
	FROM tbl_WorkItemCoreLatest WITL
	join @area A ON WITL.AreaPath=A.SrcPath
	join @Iter I ON WITL.IterationPath=I.SrcPath
	