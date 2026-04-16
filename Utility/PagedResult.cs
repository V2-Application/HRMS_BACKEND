public class PagedResult<T>
{
    public List<T> Data { get; set; }
    public int TotalRecords { get; set; }

    public PagedResult(List<T> data, int totalRecords)
    {
        Data = data;
        TotalRecords = totalRecords;
    }
}