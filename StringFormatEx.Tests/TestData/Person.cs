using System.Collections.Generic;



public class Person
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public int Age { get; set; }
    public IList<Person> Friends { get; set; }
}