using System.Data;
using System.Linq; // Добавлено для использования AsEnumerable() и Count()
using DatabaseCourseExam.Data;
using Npgsql;

namespace DatabaseCourseExam.AdoNet;

public static class DisconnectedTasks
{
    public static async Task<DataSet> LoadDataSetAsync()
    {
        var dataSet = new DataSet("School");

        await using var connection = DatabaseConnection.Create();
        await connection.OpenAsync();

        const string studentsSql = """
            SELECT id, first_name, last_name, age, group_id
            FROM students
            ORDER BY id;
            """;
        await using var studentsCmd = new NpgsqlCommand(studentsSql, connection);
        using var studentsAdapter = new NpgsqlDataAdapter(studentsCmd);
        studentsAdapter.Fill(dataSet, "Students");

        // Установка первичного ключа
        dataSet.Tables["Students"]!.PrimaryKey = [dataSet.Tables["Students"]!.Columns["id"]!];

        const string groupsSql = """
            SELECT id, name 
            FROM student_groups 
            ORDER BY id;
            """;
        await using var groupsCmd = new NpgsqlCommand(groupsSql, connection);
        using var groupsAdapter = new NpgsqlDataAdapter(groupsCmd);
        groupsAdapter.Fill(dataSet, "Groups");

        dataSet.Tables["Groups"]!.PrimaryKey = [dataSet.Tables["Groups"]!.Columns["id"]!];

        // Создание DataRelation

        DataColumn parentColumn = dataSet.Tables["Groups"]!.Columns["id"]!;
        DataColumn childColumn = dataSet.Tables["Students"]!.Columns["group_id"]!;

        var relation = new DataRelation("StudentGroupRelation", parentColumn, childColumn);
        dataSet.Relations.Add(relation);

        return dataSet;
    }

    public static void AddStudent(DataTable table, string firstName, string lastName, int age, int? groupId)
    {
        if (age < 14 || age > 100)
        {
            Console.WriteLine("Ошибка: Возраст должен быть в диапазоне от 14 до 100 лет.");
            return;
        }

        DataRow newRow = table.NewRow();
        newRow["first_name"] = firstName;
        newRow["last_name"] = lastName;
        newRow["age"] = age;
        newRow["group_id"] = groupId ?? (object)DBNull.Value; // Обработка возможного отсутствия группы

        table.Rows.Add(newRow);
        Console.WriteLine($"Студент добавлен в память. RowState: {newRow.RowState} (Ожидается: Added)");
    }

    public static void PrintTable(DataTable table)
    {
        foreach (DataRow row in table.Rows)
        {
            Console.WriteLine(
                $"{row["id"]}: {row["first_name"]} " +
                $"{row["last_name"]}, возраст {row["age"]}");
        }
    }
    public static void ChangeAgeInMemory(
        DataTable table,
        int studentId,
        int newAge)
    {
        DataRow? row = table.Rows.Find(studentId);

        if (row is null)
        {
            Console.WriteLine("Студент не найден в DataTable.");
            return;
        }

        Console.WriteLine($"RowState до изменения: {row.RowState}");

        if (Convert.ToInt32(row["age"]) == newAge)
        {
            Console.WriteLine("Возраст не изменился. Операция отменена, состояние строки не изменено.");
            return;
        }

        // Восстановлено присваивание нового возраста DataRow
        row["age"] = newAge;

        Console.WriteLine($"RowState после изменения: {row.RowState}");
    }

    public static int SaveChanges(DataTable table)
    {
        using var connection = DatabaseConnection.Create();
        connection.Open();

        const string sql = """
        UPDATE students
        SET age = @age
        WHERE id = @id;
        """;

        using var command = new NpgsqlCommand(sql, connection);

        // Параметры привязаны к колонкам DataTable по имени
        command.Parameters.Add("@age", NpgsqlTypes.NpgsqlDbType.Integer, 4, "age");
        command.Parameters.Add("@id", NpgsqlTypes.NpgsqlDbType.Integer, 4, "id");

        using var adapter = new NpgsqlDataAdapter();
        adapter.UpdateCommand = command;

        int affectedRows = adapter.Update(table);
        Console.WriteLine($"Сохранено изменений в БД: {affectedRows}");

        int modifiedCount = table.AsEnumerable().Count(r => r.RowState == DataRowState.Modified);
        Console.WriteLine($"Строк в состоянии Modified после сохранения: {modifiedCount}");

        return affectedRows;
    }
}