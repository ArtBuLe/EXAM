using DatabaseCourseExam.EntityFramework.Entities;
using Microsoft.EntityFrameworkCore;

namespace DatabaseCourseExam.EntityFramework;

public static class EntityFrameworkTasks
{
    public static async Task ShowStudentsAsync()
    {
        await using var context = SchoolDbContextFactory.Create();

        List<Student> students = await context.Students
            .Include(student => student.Group)
            .OrderBy(student => student.Id)
            .ToListAsync();

        foreach (Student student in students)
        {
            Console.WriteLine(
                $"{student.Id}. {student.FirstName} {student.LastName}, " +
                $"возраст: {student.Age}, группа: {student.Group.Name}");
        }

        Console.WriteLine("Нажмите любую кнопку чтобы продолжить...");
        Console.ReadLine();
    }

    public static async Task ShowGroupedStudents()
    {
        await using var context = SchoolDbContextFactory.Create();

        List<Student> students = await context.Students
            .Include(student => student.Group)
            .OrderBy(student => student.Id)
            .ToListAsync();

        var groupedStudents = students.GroupBy(s => s.Group?.Name ?? "Без группы");

        foreach (var group in groupedStudents)
        {
            // вывод группы и кол-ва студентов в ней
            Console.WriteLine($"\nГруппа: {group.Key} - Всего студентов: {group.Count()}");

            // вывод студентов по группе
            foreach (var student in group.OrderBy(s => s.Id))
            {
                Console.WriteLine(
                    $"  {student.Id}. {student.FirstName} {student.LastName}, " +
                    $"возраст: {student.Age}");
            }
        }

        Console.WriteLine("Нажмите любую кнопку чтобы продолжить...");
        Console.ReadLine();
    }

    public static async Task UpdateStudentAgeAsync(int studentId, int newAge)
    {
        await using var context = SchoolDbContextFactory.Create();

        Student? student = await context.Students.FirstOrDefaultAsync(x => x.Id == studentId);

        if (student is null)
        {
            Console.WriteLine("Студент не найден.");
            return;
        }

        student.Age = newAge;
        
        try
        {
            int affectedRows = await context.SaveChangesAsync();
            Console.WriteLine($"Возраст изменён. Сохранено изменений: {affectedRows}");
        }
        catch (DbUpdateException ex)
        {
            throw ex;
        }
    }

    public static async Task ShowCoursesAsync()
    {
        await using var context = SchoolDbContextFactory.Create();

        var courses = await context.Courses
            .OrderBy(course => course.Name)
            .Select(course => new
            {
                course.Name,
                course.Price,
                course.AvailablePlaces,
                // Вычисляемое свойство: true, если мест не осталось
                NoPlacesLeft = course.AvailablePlaces == 0
            })
            .ToListAsync();

        foreach (var course in courses)
        {
            // Формируем строку вывода с учётом вычисляемого признака
            string placesInfo = course.NoPlacesLeft
                ? "Свободных мест: 0 (мест нет)"
                : $"Свободных мест: {course.AvailablePlaces}";

            Console.WriteLine(
                $"{course.Name} | Цена: {course.Price:N2} | {placesInfo}");
        }
    }
}