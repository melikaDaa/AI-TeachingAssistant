using AI.TeachingAssistant.Application.Dtos;
using AI.TeachingAssistant.Application.Services;
using AI.TeachingAssistant.Domain.Entities;
using AI.TeachingAssistant.Domain.Models;

namespace AI.TeachingAssistant.Application.Mapping;

/// <summary>Maps domain entities to API DTOs. Entities never cross the API boundary.</summary>
public static class DtoMapper
{
    public static TeacherDto ToDto(this Teacher t) =>
        new(t.Id, t.FullName, t.Level, t.Grade, t.Subjects, t.TeachingStyle);

    public static LessonSummaryDto ToSummaryDto(this Lesson l) =>
        new(l.Id, l.Title, l.Subject, l.Grade, l.Topic, l.DurationMinutes, l.StudentLevel, l.PackageStatus, l.CreatedAt);

    public static LessonPlanDto? ToDto(this LessonPlan? entity)
    {
        var m = entity?.Model;
        if (m is null) return null;
        return new LessonPlanDto(
            m.LearningObjectives,
            m.RequiredMaterials,
            m.Segments.Select(s => new LessonPlanSegmentDto(s.StartMinute, s.EndMinute, s.Title, s.Description)).ToList(),
            m.Homework);
    }

    public static EducationalContentDto? ToDto(this EducationalContent? entity)
    {
        var m = entity?.Model;
        if (m is null) return null;
        return new EducationalContentDto(
            m.SimpleExplanation, m.KeyConcepts, m.Examples, m.CommonMisconceptions, m.RealWorldExample, m.Summary);
    }

    public static ActivityDto? ToDto(this ClassroomActivity? entity)
    {
        var m = entity?.Model;
        if (m is null) return null;
        return new ActivityDto(
            m.Title, m.Objective, m.Materials, m.StudentCount, m.DurationMinutes, m.Instructions, m.ExpectedResult);
    }

    public static QuizDto? ToDto(this Quiz? entity)
    {
        var m = entity?.Model;
        if (m is null) return null;
        return new QuizDto(
            entity!.Id,
            m.Questions.Select(q => new QuestionDto(
                q.Type == QuestionType.Mcq ? "mcq" : "shortAnswer",
                q.Text, q.Options, q.CorrectAnswer, q.Explanation)).ToList());
    }

    public static MediaPromptsDto ToMediaDto(this TeachingPackage? package)
    {
        var images = new List<ImagePromptDto>();
        VideoPromptDto? video = null;

        foreach (var mp in package?.MediaPrompts ?? Enumerable.Empty<MediaPrompt>())
        {
            if (mp.Kind == MediaPromptKind.Image && mp.ImageModel is not null)
            {
                var m = mp.ImageModel;
                images.Add(new ImagePromptDto(mp.Id, mp.Title, m.Subject, m.VisualElements, m.Style,
                    m.Composition, m.EducationalPurpose, m.TextRestrictions, m.FinalPrompt, m.ImageUrl));
            }
            else if (mp.Kind == MediaPromptKind.Video && mp.VideoModel is not null)
            {
                var m = mp.VideoModel;
                video = new VideoPromptDto(mp.Id, mp.Title, m.SceneDescription, m.Characters, m.Environment,
                    m.Camera, m.Motion, m.Duration, m.EducationalObjective, m.VisualStyle, m.FinalPrompt);
            }
        }

        return new MediaPromptsDto(images, video);
    }

    public static TeachingPackageDto ToPackageDto(this Lesson lesson)
    {
        var package = lesson.Package;
        return new TeachingPackageDto(
            lesson.Id,
            lesson.Title,
            new GenerationProgressDto(
                StatusKey(lesson.PackageStatus),
                ContentTypes.Names.GetValueOrDefault(CurrentSection(package), string.Empty),
                package?.CompletedSections.ToList() ?? new List<string>(),
                lesson.LastError),
            package?.LessonPlan.ToDto(),
            package?.EducationalContent.ToDto(),
            package?.Activity.ToDto(),
            package?.Quiz.ToDto(),
            package.ToMediaDto(),
            GenerationCoordinator.ParseSections(lesson.RequestedSections));
    }

    public static string StatusKey(PackageStatus status) => status switch
    {
        PackageStatus.Generating => "generating",
        PackageStatus.Ready => "ready",
        PackageStatus.Failed => "failed",
        _ => "draft",
    };

    private static int CurrentSection(TeachingPackage? package)
    {
        if (package is null) return 0;
        var done = package.CompletedSections.Select(int.Parse).DefaultIfEmpty(0).Max();
        return done == 0 ? 0 : Math.Min(done + 1, ContentTypes.VideoPrompt);
    }
}
