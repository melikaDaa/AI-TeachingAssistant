using AI.TeachingAssistant.Application.Ai;
using Xunit;

namespace AI.TeachingAssistant.Tests;

public class AiJsonTests
{
    [Fact]
    public void Extract_Returns_Json_Inside_Markdown_Fences()
    {
        var raw = "این هم خروجی شما:\n```json\n{\"a\": 1}\n```";
        Assert.Equal("{\"a\": 1}", AiJson.Extract(raw));
    }

    [Fact]
    public void Extract_Handles_Braces_Inside_Strings()
    {
        var raw = "متن {\"a\": \"شامل { و } در متن\"} پایانی";
        Assert.Equal("{\"a\": \"شامل { و } در متن\"}", AiJson.Extract(raw));
    }

    [Fact]
    public void Extract_Returns_Null_Without_Json()
    {
        Assert.Null(AiJson.Extract("پاسخی در کار نیست"));
    }

    [Fact]
    public void Parse_Returns_Null_For_Empty_Input()
    {
        Assert.Null(AiJson.Parse<AiLessonPlan>(""));
        Assert.Null(AiJson.Parse<AiLessonPlan>("   "));
    }

    [Fact]
    public void Parse_Maps_CamelCase_Properties()
    {
        var raw = "{\"learningObjectives\":[\"هدف\"],\"segments\":[{\"startMinute\":0,\"endMinute\":5,\"title\":\"گرم‌کردن\"}]}";
        var plan = AiJson.Parse<AiLessonPlan>(raw);
        Assert.NotNull(plan);
        Assert.Equal("هدف", plan!.LearningObjectives[0]);
        Assert.Equal(5, plan.Segments[0].EndMinute);
    }
}
