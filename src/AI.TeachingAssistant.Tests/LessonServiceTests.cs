using AI.TeachingAssistant.Application.Dtos;
using AI.TeachingAssistant.Application.Common;
using AI.TeachingAssistant.Application.Services;
using AI.TeachingAssistant.Domain.Entities;
using Xunit;

namespace AI.TeachingAssistant.Tests;

public class LessonServiceTests
{
    private static CreateLessonRequest ValidRequest() => new()
    {
        Title = "سلول چیست؟",
        Subject = "علوم",
        Grade = "هفتم",
        Topic = "سلول",
        DurationMinutes = 60,
        StudentLevel = "متوسط",
    };

    [Fact]
    public void Rejects_Invalid_Duration()
    {
        var request = ValidRequest();
        request.DurationMinutes = 5;
        Assert.Throws<ValidationException>(() => LessonService.ValidateLesson(
            request.Title, request.Subject, request.Grade, request.Topic, request.DurationMinutes, request.StudentLevel));
    }

    [Fact]
    public void Rejects_Unknown_Student_Level()
    {
        Assert.Throws<ValidationException>(() => LessonService.ValidateLesson("عنوان", "علوم", "هفتم", "سلول", 60, "نامشخص"));
    }

    [Fact]
    public void Rejects_Empty_Topic()
    {
        Assert.Throws<ValidationException>(() => LessonService.ValidateLesson("عنوان", "علوم", "هفتم", "  ", 60, "متوسط"));
    }

    [Fact]
    public void NormalizeSections_Defaults_To_All_Six()
    {
        Assert.Equal("1,2,3,4,5,6", LessonService.NormalizeSections(null));
        Assert.Equal("1,2,3,4,5,6", LessonService.NormalizeSections(new List<int>()));
        Assert.Equal("1,2,3,4,5,6", LessonService.NormalizeSections(new List<int> { 99 }));
    }

    [Fact]
    public void NormalizeSections_Keeps_Only_Valid_Unique_Codes()
    {
        Assert.Equal("1,4", LessonService.NormalizeSections(new List<int> { 1, 4, 4, 55 }));
    }
}
