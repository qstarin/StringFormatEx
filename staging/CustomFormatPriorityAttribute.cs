using System;



[AttributeUsageAttribute(AttributeTargets.Method)]
public class CustomFormatPriorityAttribute : Attribute
{
	public CustomFormatPriorities Priority;
	public CustomFormatPriorityAttribute(CustomFormatPriorities newPriority = CustomFormatPriorities.Normal)
	{
		this.Priority = newPriority;
	}
}
