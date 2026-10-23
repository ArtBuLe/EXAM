using Dapper;
using DatabaseCourseExam.Data;
using Npgsql;
using NpgsqlTypes;

namespace DatabaseCourseExam.AdoNet;

public static class AdoNetTasks
{
    public static async Task ShowStudentsAsync()
    {
        await using var connection = DatabaseConnection.Create();
        await connection.OpenAsync();

        // считываем группу
        Console.Write("Введите ID группы для фильтрации (или нажмите Enter, чтобы показать всех): ");
        string input = Console.ReadLine()!;

        string sql;
        await using var command = new NpgsqlCommand();
        command.Connection = connection;

        if (int.TryParse(input, out int groupId))
        {
            // фильтр
            sql = """
            SELECT s.id, s.first_name, s.last_name, s.age, g.name
            FROM students AS s
            JOIN student_groups AS g ON g.id = s.group_id
            WHERE s.group_id = @GroupId
            ORDER BY s.id;
            """;
            command.CommandText = sql;

            // передаем параметром
            command.Parameters.AddWithValue("GroupId", groupId);
        }
        else
        {
            // показ всех
            sql = """
            SELECT s.id, s.first_name, s.last_name, s.age, g.name
            FROM students AS s
            JOIN student_groups AS g ON g.id = s.group_id
            ORDER BY s.id;
            """;
            command.CommandText = sql;
        }

        await using var reader = await command.ExecuteReaderAsync();

        bool hasData = false;
        while (await reader.ReadAsync())
        {
            hasData = true;
            // проверка на нулл прежде чем вызывать GetString/GetInt32
            string id = reader.IsDBNull(0) ? "N/A" : reader.GetInt32(0).ToString();
            string firstName = reader.IsDBNull(1) ? "Не указано" : reader.GetString(1);
            string lastName = reader.IsDBNull(2) ? "Не указано" : reader.GetString(2);
            string age = reader.IsDBNull(3) ? "N/A" : reader.GetInt32(3).ToString();
            string groupName = reader.IsDBNull(4) ? "Без группы" : reader.GetString(4);

            Console.WriteLine(
                $"{id}. " +
                $"{firstName} {lastName}, " +
                $"возраст: {age}, " +
                $"группа: {groupName}");
        }

        if (!hasData)
        {
            Console.WriteLine("Студенты не найдены.");
        }

        Console.WriteLine("Нажмите любую кнопку чтобы продолжить...");
        Console.ReadLine();
    }

    public static async Task UpdateStudentAgeAsync(int studentId, int newAge)
    {
        if (newAge < 14 || newAge > 100)
        {
            Console.WriteLine("Ошибка: Возраст должен быть в диапазоне от 14 до 100 лет.");
            return;
        }
        await using var connection = DatabaseConnection.Create();
        await connection.OpenAsync();

        const string sql = 
            """
            UPDATE students
            SET age = @Age
            WHERE id = @StudentId;
            """;

        await using var command = new NpgsqlCommand(sql, connection);

        command.Parameters.Add("@Age", NpgsqlDbType.Integer).Value = newAge;
        command.Parameters.Add("@StudentId", NpgsqlDbType.Integer).Value =
            studentId;

        int affectedRows = await command.ExecuteNonQueryAsync();

        if (affectedRows == 0)
        {
            Console.WriteLine("Студент не найден.");
        }
        else
        {
            Console.WriteLine($"Изменено записей: {affectedRows}");
        }
    }

    public static async Task EnrollStudentAsync(
    int studentId,
    int courseId)
    {
        await using var connection = DatabaseConnection.Create();
        await connection.OpenAsync();

        // Начинаем транзакцию, чтобы все операции были атомарными
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // проверка студента
            const string checkStudentSql = "SELECT COUNT(1) FROM students WHERE id = @StudentId;";
            int studentExists = await connection.ExecuteScalarAsync<int>(
                checkStudentSql,
                new { StudentId = studentId },
                transaction);

            if (studentExists == 0)
            {
                throw new InvalidOperationException("Студент не найден.");
            }

            // проверка студента
            const string checkEnrollmentSql =
                "SELECT COUNT(1) FROM student_courses WHERE student_id = @StudentId AND course_id = @CourseId;";
            int alreadyEnrolled = await connection.ExecuteScalarAsync<int>
                (
                checkEnrollmentSql,
                new { StudentId = studentId, CourseId = courseId },
                transaction
                );

            if (alreadyEnrolled > 0)
            {
                throw new InvalidOperationException("Студент уже записан на этот курс.");
            }

            // проверка мест
            const string updateSql =
            """
            UPDATE courses
            SET available_places = available_places - 1
            WHERE id = @CourseId
              AND available_places > 0
            RETURNING available_places;
            """;

            int? remainingPlaces = await connection.QuerySingleOrDefaultAsync<int?>(
                updateSql,
                new { CourseId = courseId },
                transaction);

            if (remainingPlaces is null)
            {
                throw new InvalidOperationException("Курс не найден или свободных мест нет.");
            }

            // insert
            const string insertSql =
            """
            INSERT INTO student_courses (student_id, course_id)
            VALUES (@StudentId, @CourseId);
            """;

            await connection.ExecuteAsync(
                insertSql,
                new { StudentId = studentId, CourseId = courseId },
                transaction);

            // фиксация
            await transaction.CommitAsync();

            Console.WriteLine($"Студент успешно записан на курс. Осталось мест: {remainingPlaces}");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Console.WriteLine($"Операция отменена: {ex.Message}");
        }
    }
}