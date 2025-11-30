namespace TodoListApp.WebApi.Models.Tags;

public class TagDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class AddTagDto
{
    public string Name { get; set; } = "";
}
public class UpdateTagDto
{
    public string Name { get; set; } = "";
}
