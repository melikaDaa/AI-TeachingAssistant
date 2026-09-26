using AI.TeachingAssistant.Application.Ai;
using AI.TeachingAssistant.Application.Common;
using AI.TeachingAssistant.Domain.Models;

namespace AI.TeachingAssistant.Infrastructure.Ai;

/// <summary>
/// Deterministic offline provider used for development and demo. It parses the section JSON schema
/// out of the user prompt's example and returns a valid filled sample, so the whole product works
/// without any API key or cost. Real providers implement the same IAIProvider interface.
/// </summary>
public class MockAIProvider : IAIProvider
{
    public string Name => "Mock (آفلاین)";

    public Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var json = MockContentFactory.Build(request.SystemPrompt, request.UserPrompt);
        return Task.FromResult(new AiResponse(json));
    }
}

/// <summary>Builds valid Persian sample content for each section based on the lesson topic.</summary>
public static class MockContentFactory
{
    public static string Build(string systemPrompt, string userPrompt)
    {
        var ctx = MockLessonContext.Parse(userPrompt);

        if (userPrompt.Contains("خواسته: طرح درس"))
            return Payload(PayloadJson.Serialize(BuildLessonPlan(ctx)));
        if (userPrompt.Contains("خواسته: محتوای آموزشی"))
            return Payload(PayloadJson.Serialize(BuildContent(ctx)));
        if (userPrompt.Contains("خواسته: فعالیت کلاسی"))
            return Payload(PayloadJson.Serialize(BuildActivity(ctx)));
        if (userPrompt.Contains("خواسته: آزمون"))
            return Payload(PayloadJson.Serialize(BuildQuiz(ctx)));
        if (userPrompt.Contains("خواسته: پرامپت تصویر"))
            return Payload(PayloadJson.Serialize(BuildImagePrompts(ctx)));
        if (userPrompt.Contains("خواسته: پرامپت ویدئو"))
            return Payload(PayloadJson.Serialize(BuildVideoPrompt(ctx)));

        return Payload("{}");
    }

    private static string Payload(string json) => "```json\n" + json + "\n```";

    private sealed record MockLessonContext(string Topic, string Subject, string Grade, int Duration, string Level, string Style, string TeacherName)
    {
        public static MockLessonContext Parse(string prompt)
        {
            string Field(string label)
            {
                var start = prompt.IndexOf(label, StringComparison.Ordinal);
                if (start < 0) return string.Empty;
                start += label.Length;
                var end = prompt.IndexOf('\n', start);
                var value = end < 0 ? prompt[start..].Trim() : prompt[start..end].Trim().TrimEnd('؛', '.');
                return value.TrimStart('-').Trim();
            }

            var duration = 60;
            var rawDuration = Field("مدت کلاس:");
            var digits = new string(rawDuration.TakeWhile(char.IsDigit).ToArray());
            if (int.TryParse(digits, out var d)) duration = d;

            return new MockLessonContext(
                Field("موضوع:"),
                Field("درس:"),
                Field("پایه:"),
                duration,
                Field("سطح دانش‌آموزان:"),
                Field("سبک تدریس:"),
                Field("نام:"));
        }
    }

    private static AiLessonPlan BuildLessonPlan(MockLessonContext c)
    {
        var segs = new List<(int s, int e, string t, string d)>();
        // Realistic schedule inside the class duration: warm-up 10%, intro 25%, main 30%, activity 25%, assess+summary 10%.
        var warm = Math.Max(3, c.Duration / 10);
        var intro = c.Duration / 4;
        var main = c.Duration / 3;
        var act = c.Duration / 4;
        var end = c.Duration - warm - intro - main - act;

        segs.Add((0, warm, "گرم‌کردن و طرح پرسش", $"پرسش آغازین درباره «{c.Topic}» و شنیدن حدس‌های دانش‌آموزان."));
        segs.Add((warm, warm + intro, "معرفی مفهوم", $"معرفی کلیدواژه‌های «{c.Topic}» با مثال‌های ساده و روزمره."));
        segs.Add((warm + intro, warm + intro + main, "تدریس مفهوم اصلی", $"توضیح گام‌به‌گام «{c.Topic}» همراه با پرسش و پاسخ کلاسی."));
        segs.Add((warm + intro + main, warm + intro + main + act, "فعالیت گروهی", $"دانش‌آموزان در گروه‌های کوچک نمونه‌های «{c.Topic}» را بررسی و ارائه می‌کنند."));
        segs.Add((warm + intro + main + act, c.Duration, "ارزشیابی و جمع‌بندی", "چند پرسش کوتاه از کل کلاس و جمع‌بندی نکات اصلی روی تخته."));

        return new AiLessonPlan
        {
            LearningObjectives =
            {
                $"دانش‌آموز بتواند «{c.Topic}» را به زبان خود توضیح دهد.",
                $"دانش‌آموز بتواند دو مثال واقعی از «{c.Topic}» نام ببرد.",
                "دانش‌آموز بتواند پرسش‌های پایانی کلاس را درست پاسخ دهد.",
            },
            RequiredMaterials = { "تخته و ماژیک", $"تصاویر یا نمودار مرتبط با «{c.Topic}»", "برگه فعالیت گروهی", "وایت‌برد کوچک برای هر گروه" },
            Segments = segs.Select(x => new AiSegment { StartMinute = x.s, EndMinute = x.e, Title = x.t, Description = x.d }).ToList(),
            Homework = $"از دانش‌آموزان بخواهید یک مثال واقعی از «{c.Topic}» در خانه پیدا کرده و در جلسه بعد توضیح دهند.",
        };
    }

    private static AiEducationalContent BuildContent(MockLessonContext c) => new()
    {
        SimpleExplanation = $"«{c.Topic}» یکی از موضوع‌های مهم درس {c.Subject} در پایه {c.Grade} است. " +
            "برای اینکه خوب بفهمیم، اول باید بدانیم این موضوع چه چیزی را بررسی می‌کند و بعد رابطه‌اش را با چیزهایی که هر روز می‌بینیم پیدا کنیم. " +
            "تصور کنید کلاس شما یک شهر کوچک است؛ هر دانش‌آموز یک وظیفه دارد و همه با هم کلاس را سرپا نگه می‌دارند. " +
            $"«{c.Topic}» هم دقیقاً همین‌طور کار می‌کند: اجزای کوچکی که هر کدام کاری انجام می‌دهند و کنار هم یک کل منظم می‌سازند. " +
            "اگر یکی از اجزا درست کار نکند، کل مجموعه دچار مشکل می‌شود؛ به همین دلیل شناخت هر بخش اهمیت دارد.",
        KeyConcepts = { $"تعریف ساده «{c.Topic}»", "اجزای اصلی و نقش هر جزء", "رابطه اجزا با یکدیگر", "کاربرد موضوع در زندگی روزمره" },
        Examples = { $"مثال اول: یک نمونه آشنا از محیط مدرسه که شباهت زیادی به «{c.Topic}» دارد.", "مثال دوم: یک تجربه روزمره (مانند آشپزخانه یا مسیر خانه تا مدرسه) که همین ساختار را نشان می‌دهد." },
        CommonMisconceptions = { $"اشتباه رایج: تصور اینکه «{c.Topic}» فقط یک تعریف حفظی است و کاربردی ندارد.", "اشتباه رایج: یکسان‌گرفتن اجزای مختلف و ندیدن نقش متمایز هر جزء." },
        RealWorldExample = $"در روزهای بارانی، رفت‌وآمد در مدرسه نظم خاصی پیدا می‌کند؛ همین نظم، نمونه‌ای ساده از چگونگی کارکرد «{c.Topic}» است.",
        Summary = $"«{c.Topic}» از اجزایی ساخته شده که هر کدام وظیفه‌ای دارند و در کنار هم یک کل منظم می‌سازند؛ فهمیدن نقش هر جزء، یادگیری را آسان می‌کند.",
    };

    private static AiActivity BuildActivity(MockLessonContext c)
    {
        var minutes = Math.Max(10, Math.Min(c.Duration / 4, 30));
        return new AiActivity
        {
            Title = $"کارگاه گروهی «{c.Topic}»",
            Objective = $"تثبیت مفهوم «{c.Topic}» از طریق ساخت و ارائه نمونه در گروه.",
            Materials = { "کارت‌های موضوعی", "ماژیک و کاغذ بزرگ", "چسب و قیچی", "خمیر یا مقوا" },
            StudentCount = "گروه‌های ۴ نفره (مناسب کلاس ۳۰ نفره)",
            DurationMinutes = minutes,
            Instructions =
            {
                $"کلاس را به گروه‌های ۴ نفره تقسیم کنید و به هر گروه یک کارت موضوعی درباره «{c.Topic}» بدهید.",
                "هر گروه روی کاغذ بزرگ، نمونه خود را می‌کشد یا می‌سازد و هر جزء را برچسب می‌زند.",
                $"گروه‌ها در دو دقیقه، ساخته خود و ربط آن به «{c.Topic}» را برای کلاس ارائه می‌کنند.",
                "هر گروه پس از ارائه، یک پرسش از گروه بعدی می‌پرسد.",
                $"در پایان، معلم روی تخته نکات مشترک همه گروه‌ها درباره «{c.Topic}» را جمع‌بندی می‌کند.",
            },
            ExpectedResult = "همه گروه‌ها نمونه خود را ارائه می‌دهند و دانش‌آموزان حداقل سه نکته کلیدی را در جمع‌بندی بازگو می‌کنند.",
        };
    }

    private static AiQuiz BuildQuiz(MockLessonContext c)
    {
        var mcq = new List<AiQuestion>();
        var correctSets = new[]
        {
            ("کدام گزینه بهترین توصیف برای موضوع درس است؟", "همان تعریف درستِ مطرح‌شده در کلاس", "تعریفی بی‌ربط", "یک مثال اشتباه", "هیچ‌کدام"),
            ("رابطه اجزای موضوع با هم چگونه است؟", "هر جزء نقش مشخصی دارد و کنار هم کل منظم می‌سازند", "هیچ رابطه‌ای ندارند", "همه اجزا یکسان‌اند", "فقط یک جزء مهم است"),
            ("کدام مورد یک مثال درست از موضوع درس است؟", "نمونه مطرح‌شده در بحث کلاس", "یک نمونه بی‌ربط", "یک مثال از فصل دیگر", "هیچ‌کدام از موارد"),
            ("اگر یکی از اجزا درست کار نکند چه می‌شود؟", "کل مجموعه دچار مشکل می‌شود", "چیزی تغییر نمی‌کند", "مجموعه قوی‌تر می‌شود", "فقط آن جزء خراب می‌ماند"),
            ("بهترین راه یادگیری این موضوع کدام است؟", "مطالعه همراه با مثال و فعالیت عملی", "فقط حفظ کردن", "بدون تمرین", "فقط گوش دادن"),
        };
        for (var i = 0; i < correctSets.Length; i++)
        {
            var (text, a, b, cc, dd) = correctSets[i];
            // Vary the answer position so the key is not always گزینه الف.
            var optA = i == 3 ? "الف) " + b : "الف) " + a;
            var optB = i == 3 ? "ب) " + a : "ب) " + b;
            var correctAnswer = i == 3 ? "ب) " + a : "الف) " + a;
            mcq.Add(new AiQuestion
            {
                Type = "mcq",
                Text = text,
                Options = new List<string> { optA, optB, "ج) " + cc, "د) " + dd },
                CorrectAnswer = correctAnswer,
            });
        }

        var shortAnswers = new List<AiQuestion>
        {
            new() { Type = "shortAnswer", Text = $"«{c.Topic}» را در یک تا دو جمله توضیح دهید.", CorrectAnswer = "پاسخ نمونه: توضیح کوتاهی که تعریف و نقش اجزا را بگوید." },
            new() { Type = "shortAnswer", Text = "دو مثال واقعی از زندگی روزمره بنویسید.", CorrectAnswer = "پاسخ نمونه: هر دو مثال باید مرتبط با موضوع درس باشند." },
            new() { Type = "shortAnswer", Text = "چرا شناخت اجزای موضوع اهمیت دارد؟", CorrectAnswer = "پاسخ نمونه: چون هر جزء نقشی دارد و خرابی آن بر کل مجموعه اثر می‌گذارد." },
        };

        var questions = new List<AiQuestion>();
        questions.AddRange(mcq);
        questions.AddRange(shortAnswers);
        return new AiQuiz { Questions = questions };
    }

    private static AiImagePrompts BuildImagePrompts(MockLessonContext c) => new()
    {
        Prompts =
        {
            new AiImagePrompt
            {
                Title = $"نمودار کلی «{c.Topic}»",
                Subject = $"educational diagram of {c.Topic} for grade {c.Grade}",
                VisualElements = "labeled parts as icons, arrows showing relationships, soft color palette",
                Style = "clean educational infographic, flat vector illustration, soft pastel colors",
                Composition = "centered main object with clearly separated sections, generous white space, 16:9",
                EducationalPurpose = $"Helps students see the whole structure of {c.Topic} at a glance",
                TextRestrictions = "no text, no letters, no numbers inside the image",
                FinalPrompt = $"Clean flat-vector educational infographic about {c.Topic} for a grade {c.Grade} classroom: a central structure with clearly separated parts, arrows showing relationships, soft pastel palette, white background, generous white space, 16:9, no text, no letters, no numbers.",
            },
            new AiImagePrompt
            {
                Title = $"مقایسه «{c.Topic}» با یک نمونه واقعی",
                Subject = $"side-by-side comparison of {c.Topic} and a real-life example",
                VisualElements = "two panels: left the concept, right a familiar everyday scene, matching colors",
                Style = "friendly digital illustration for school, soft shading, warm colors",
                Composition = "split composition with a clear vertical divider, balanced halves, 4:3",
                EducationalPurpose = $"Connects the abstract concept of {c.Topic} to students' daily life",
                TextRestrictions = "no text, no letters, no numbers inside the image",
                FinalPrompt = $"Friendly educational digital illustration for school: split composition comparing the concept of {c.Topic} (left half) with a familiar everyday scene (right half), matching warm colors, soft shading, clear vertical divider, 4:3, no text, no letters, no numbers.",
            },
        },
    };

    private static AiVideoPrompt BuildVideoPrompt(MockLessonContext c) => new()
    {
        Title = $"ویدئوی کوتاه معرفی «{c.Topic}»",
        SceneDescription = $"A short educational animation introducing {c.Topic}: parts appear one by one and connect into a complete, organized whole.",
        Characters = "No human characters; friendly geometric icons representing the parts of the topic",
        Environment = "Soft minimal classroom-style backdrop with subtle notebook paper texture",
        Camera = "Slow zoom-in at the start, gentle pan following each part as it appears",
        Motion = "Parts slide in smoothly, arrows animate to show relationships, final assembled structure glows softly",
        Duration = "30 seconds",
        EducationalObjective = $"Students get an intuitive first picture of how {c.Topic} works as a whole",
        VisualStyle = "Flat vector animation, soft pastel palette, calm and clean, suitable for grade " + c.Grade,
        FinalPrompt = $"30-second flat-vector educational animation introducing {c.Topic} for grade {c.Grade} students: friendly geometric icons slide in one by one, arrows animate to show relationships between parts, slow zoom-in camera with gentle pans, soft pastel palette on a minimal notebook-paper backdrop, calm and clean style, the parts finally assemble into one organized whole, no text, no captions, no voiceover.",
    };
}
