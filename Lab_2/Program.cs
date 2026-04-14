using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SimplexMethodDual
{
    // Клас для збереження виразів віддалених двоїстих змінних
    class DualExpression
    {
        public string TargetVar;
        public Dictionary<string, double> Coefficients = new Dictionary<string, double>();
        public double FreeTerm;
    }

    class Program
    {
        static List<string> currentProtocol = new List<string>();

        static void Main(string[] args)
        {
            Console.WriteLine("РОЗВ'ЯЗАННЯ ДВОЇСТОЇ ЗАДАЧІ ЗА ДОПОМОГОЮ МЖВ");
            SolveLPPMenu(true);
        }

        static void Log(string message, bool writeLine = true)
        {
            if (writeLine)
            {
                Console.WriteLine(message);
                currentProtocol.Add(message);
            }
            else
            {
                Console.Write(message);
                if (currentProtocol.Count == 0) currentProtocol.Add("");
                currentProtocol[currentProtocol.Count - 1] += message;
            }
        }

        static void LogMatrixLPP(double[,] matrix, string[] rowHeaders, string[] colHeaders)
        {
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);

            string header = "        ";
            for (int j = 0; j < cols; j++) header += $"{colHeaders[j],8} ";
            Log(header);

            for (int i = 0; i < rows; i++)
            {
                string rowStr = $"{rowHeaders[i],6} =";
                for (int j = 0; j < cols; j++)
                {
                    rowStr += $"{matrix[i, j],8:F2} ";
                }
                Log(rowStr);
            }
            Log("");
        }

        static void PromptSaveToFile()
        {
            Console.Write("\nБажаєте зберегти цей протокол у текстовий файл? (т/н): ");
            string answer = Console.ReadLine()?.Trim().ToLower();
            if (answer == "т" || answer == "y" || answer == "так")
            {
                string filename = $"Protocol_ZHV_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                File.WriteAllLines(filename, currentProtocol);
                Console.WriteLine($"\nФайл успішно збережено під назвою: {filename}");
            }
        }

        static double[] ReadVector(int n)
        {
            double[] vector = new double[n];
            while (true)
            {
                string[] inputs = Console.ReadLine().Trim().Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (inputs.Length == n)
                {
                    bool valid = true;
                    for (int i = 0; i < n; i++)
                    {
                        if (!double.TryParse(inputs[i].Replace('.', ','), out vector[i]))
                        {
                            valid = false;
                            break;
                        }
                    }
                    if (valid) return vector;
                }
                else Console.WriteLine($"Помилка: потрібно ввести рівно {n} чисел (через пробіл). Спробуйте ще раз.");
            }
        }

        static double[,] DoSimplexMJEStep(double[,] matrix, int r, int s)
        {
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);
            double[,] next = new double[rows, cols];
            double pivot = matrix[r, s];

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    if (i == r && j == s) next[i, j] = 1.0;
                    else if (i == r) next[i, j] = matrix[r, j];
                    else if (j == s) next[i, j] = -matrix[i, s];
                    else next[i, j] = matrix[i, j] * pivot - matrix[i, s] * matrix[r, j];

                    next[i, j] /= pivot;
                }
            }
            return next;
        }

        static void SwapLPPHeaders(ref string rowHeader, ref string colHeader)
        {
            string[] colParts = colHeader.Split(new char[] { ',' }, 2);
            string c1 = colParts[0].Trim();
            string c2 = colParts.Length > 1 ? colParts[1].Trim().Replace("-", "") : "";

            string[] rowParts = rowHeader.Split(new char[] { ' ' }, 2);
            string r1 = rowParts[0].Trim();
            string r2 = rowParts.Length > 1 ? rowParts[1].Trim() : "";

            rowHeader = string.IsNullOrEmpty(c2) ? c1 : $"{c1} {c2}";
            colHeader = string.IsNullOrEmpty(r2) ? r1 : $"{r1}, -{r2}";
        }

        static double[,] RemoveColumn(double[,] matrix, int colIndex)
        {
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);
            double[,] newMatrix = new double[rows, cols - 1];
            for (int i = 0; i < rows; i++)
            {
                int newCol = 0;
                for (int j = 0; j < cols; j++)
                {
                    if (j == colIndex) continue;
                    newMatrix[i, newCol] = matrix[i, j];
                    newCol++;
                }
            }
            return newMatrix;
        }

        static string[] RemoveHeader(string[] headers, int index)
        {
            string[] newHeaders = new string[headers.Length - 1];
            int newIdx = 0;
            for (int i = 0; i < headers.Length; i++)
            {
                if (i == index) continue;
                newHeaders[newIdx] = headers[i];
                newIdx++;
            }
            return newHeaders;
        }

        static void PrintSolutionX(double[,] table, string[] rowHeaders, int originalN)
        {
            double[] X = new double[originalN];
            int colsCount = table.GetLength(1) - 1;
            for (int j = 0; j < originalN; j++)
            {
                string varName = $"x{j + 1}";
                X[j] = 0;
                for (int i = 0; i < rowHeaders.Length - 1; i++)
                {
                    if (rowHeaders[i].Split(new char[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries).Contains(varName))
                    {
                        X[j] = table[i, colsCount];
                        break;
                    }
                }
            }
            Log("X = (" + string.Join("; ", X.Select(v => $"{v:F2}")) + ")");
        }

        static void PrintDualSolutions(double[,] table, string[] rowHeaders, string[] colHeaders, int originalN, int originalM, List<DualExpression> eliminatedVars)
        {
            Dictionary<string, double> dualVals = new Dictionary<string, double>();
            int m = table.GetLength(0) - 1;
            int colsCount = table.GetLength(1) - 1;

            for (int j = 0; j < colsCount; j++)
            {
                string colVar = colHeaders[j].Replace("-", "").Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                dualVals[colVar] = table[m, j];
            }

            for (int i = 0; i < m; i++)
            {
                string rowVar = rowHeaders[i].Replace("-", "").Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                dualVals[rowVar] = 0.0;
            }

            for (int k = eliminatedVars.Count - 1; k >= 0; k--)
            {
                var expr = eliminatedVars[k];
                double val = expr.FreeTerm;
                foreach (var kvp in expr.Coefficients)
                {
                    if (dualVals.ContainsKey(kvp.Key)) val += kvp.Value * dualVals[kvp.Key];
                }
                dualVals[expr.TargetVar] = val;
            }

            double[] U = new double[originalM];
            for (int i = 0; i < originalM; i++)
            {
                string key = $"u{i + 1}";
                U[i] = dualVals.ContainsKey(key) ? dualVals[key] : 0.0;
            }

            Log("U = (" + string.Join("; ", U.Select(v => $"{v:F2}")) + ")");
        }
        static void SolveLPPMenu(bool isMixed)
        {
            Console.Write("Введіть кількість змінних (n): ");
            if (!int.TryParse(Console.ReadLine(), out int n) || n <= 0) return;
            int originalN = n;

            Console.Write("Введіть кількість обмежень (m): ");
            if (!int.TryParse(Console.ReadLine(), out int m) || m <= 0) return;
            int originalM = m;

            Console.Write("Введіть тип задачі (1 - max, 2 - min): ");
            bool isMax = Console.ReadLine().Trim() == "1";

            Console.WriteLine("\nВведіть коефіцієнти цільової функції Z (через пробіл):");
            double[] Z_coeffs = ReadVector(n);

            double[,] table = new double[m + 1, n + 1];
            string[] rowHeaders = new string[m + 1];
            string[] colHeaders = new string[n + 1];

            double[,] A = new double[m, n];
            double[] B = new double[m];
            string[] signs = new string[m];

            for (int j = 0; j < n; j++) colHeaders[j] = $"v{j + 1}, -x{j + 1}";
            colHeaders[n] = "W, 1";

            int y_counter = 1;
            List<string> freeUvars = new List<string>();
            List<DualExpression> eliminatedExpressions = new List<DualExpression>();

            Log("\nПостановка прямої задачі:");
            string zFunc = "Z = " + string.Join(" + ", Z_coeffs.Select((val, idx) => $"{val}x{idx + 1}")).Replace("+ -", "- ") + (isMax ? " -> max" : " -> min");
            Log(zFunc.Replace("1x", "x")); // Дрібне візуальне виправлення для 1x1 -> x1
            Log("при обмеженнях:");

            bool hasZeroRows = false;

            for (int i = 0; i < m; i++)
            {
                Console.WriteLine($"\nОбмеження {i + 1}:");
                Console.WriteLine("Введіть коефіцієнти при змінних (через пробіл):");
                double[] a = ReadVector(n);

                string sign = "";
                if (isMixed)
                {
                    Console.Write("Знак (<=, >= або =): ");
                    sign = Console.ReadLine().Trim();
                }
                else
                {
                    sign = "<=";
                }

                Console.Write("Вільний член (b): ");
                double b = double.Parse(Console.ReadLine().Replace('.', ','));

                for (int j = 0; j < n; j++) A[i, j] = a[j];
                B[i] = b;
                signs[i] = sign;

                string constrStr = string.Join(" + ", a.Select((val, idx) => $"{val}x{idx + 1}")).Replace("+ -", "- ") + $" {sign} {b}";
                Log(constrStr.Replace("1x", "x"));

                double multiplier = 1.0;
                if (sign == ">=") multiplier = -1.0;
                if (sign == "=" && b * multiplier < 0) multiplier = -1.0;

                for (int j = 0; j < n; j++) table[i, j] = a[j] * multiplier;
                table[i, n] = b * multiplier;

                if (sign == "=")
                {
                    rowHeaders[i] = $"u{i + 1} 0";
                    freeUvars.Add($"u{i + 1}");
                    hasZeroRows = true;
                }
                else
                {
                    rowHeaders[i] = $"u{i + 1} y{y_counter}";
                    y_counter++;
                }
            }
            Log($"x[j] >= 0, j=1,{n}");

            rowHeaders[m] = "1 Z";
            for (int j = 0; j < n; j++) table[m, j] = isMax ? -Z_coeffs[j] : Z_coeffs[j];
            table[m, n] = 0;

            Log("\nПерепишемо систему обмежень прямої задачі:");
            for (int i = 0; i < m; i++)
            {
                string eq = "";
                for (int j = 0; j < n; j++)
                {
                    double val = -table[i, j];
                    eq += (val < 0 ? $"({val:F2})" : $"{val:F2}") + $" * X[{j + 1}] + ";
                }
                double freeTerm = table[i, n];
                eq += (freeTerm < 0 ? $"({freeTerm:F2})" : $"{freeTerm:F2}") + " ";

                if (rowHeaders[i].Contains("0")) eq += "= 0";
                else eq += ">= 0";
                Log(eq.Replace("+ (-0,00) *", "+ 0,00 *").Replace("-0,00 *", "0,00 *"));
            }

            Log("\nВхідна симплекс-таблиця для пари взаємно двоїстих задач:");
            LogMatrixLPP(table, rowHeaders, colHeaders);

            Log("Постановка двоїстої задачі:");

            // ВИПРАВЛЕНО 1: Беремо значення table[i, n], щоб врахувати знаки після зведення
            string wFunc = "W = " + string.Join(" + ", Enumerable.Range(0, m).Select(idx => {
                double val = table[idx, n];
                return val < 0 ? $"({val:F2}) * u{idx + 1}" : $"{val:F2} * u{idx + 1}";
            })) + " -> min";
            Log(wFunc);

            Log("при обмеженнях:");
            for (int j = 0; j < n; j++)
            {
                string vEq = $"v{j + 1} = ";
                for (int i = 0; i < m; i++)
                {
                    // ВИПРАВЛЕНО 2: Беремо table[i, j] замість оригінального A[i, j]
                    double coeff = table[i, j];

                    if (i > 0) vEq += " + ";
                    vEq += coeff < 0 ? $"({coeff:F2}) * u{i + 1}" : $"{coeff:F2} * u{i + 1}";
                }
                double freeC = isMax ? -Z_coeffs[j] : Z_coeffs[j];
                vEq += freeC < 0 ? $" + ({freeC:F2}) >= 0" : $" + {freeC:F2} >= 0";

                Log(vEq);
            }
            if (freeUvars.Count > 0)
            {
                Log("Вільні змінні:");
                Log(string.Join(", ", freeUvars));
            }

            if (hasZeroRows)
            {
                Log("\nВидалення нуль-рядків:");
                while (true)
                {
                    int colsCount = table.GetLength(1) - 1;
                    int r0 = -1;
                    for (int i = 0; i < m; i++)
                    {
                        if (rowHeaders[i].Contains("0")) { r0 = i; break; }
                    }

                    if (r0 == -1)
                    {
                        Log("Всі нуль-рядки видалено.");
                        break;
                    }

                    int s = -1;
                    for (int j = 0; j < colsCount; j++)
                    {
                        if (Math.Abs(Math.Abs(table[r0, j]) - 1.0) < 1e-9) { s = j; break; }
                    }

                    if (s == -1)
                    {
                        for (int j = 0; j < colsCount; j++)
                        {
                            if (Math.Abs(table[r0, j]) > 1e-9) { s = j; break; }
                        }
                    }

                    int pivotRow = r0;
                    Log($"Розв'язувальний рядок: {rowHeaders[pivotRow].Split(' ').Last()}");
                    Log($"Розв'язувальний стовпець: {colHeaders[s].Split(',').Last().Trim()}");

                    string uVar = rowHeaders[pivotRow].Replace("-", "").Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                    DualExpression expr = new DualExpression { TargetVar = uVar };
                    double pivotVal = table[pivotRow, s];

                    for (int i = 0; i < m; i++)
                    {
                        if (i == pivotRow) continue;
                        double coeff = -table[i, s] / pivotVal;
                        if (Math.Abs(coeff) > 1e-9)
                        {
                            string rowVar = rowHeaders[i].Replace("-", "").Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                            expr.Coefficients[rowVar] = coeff;
                        }
                    }

                    string colVar = colHeaders[s].Replace("-", "").Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                    double colCoeff = 1.0 / pivotVal;
                    if (Math.Abs(colCoeff) > 1e-9)
                    {
                        expr.Coefficients[colVar] = colCoeff;
                    }

                    expr.FreeTerm = -table[m, s] / pivotVal;
                    eliminatedExpressions.Add(expr);

                    table = DoSimplexMJEStep(table, pivotRow, s);
                    SwapLPPHeaders(ref rowHeaders[pivotRow], ref colHeaders[s]);

                    int colToDelete = s;
                    table = RemoveColumn(table, colToDelete);
                    colHeaders = RemoveHeader(colHeaders, colToDelete);

                    LogMatrixLPP(table, rowHeaders, colHeaders);
                }

                for (int k = eliminatedExpressions.Count - 1; k >= 0; k--)
                {
                    var sourceExpr = eliminatedExpressions[k];
                    for (int j = k - 1; j >= 0; j--)
                    {
                        var targetExpr = eliminatedExpressions[j];

                        if (targetExpr.Coefficients.ContainsKey(sourceExpr.TargetVar))
                        {
                            double multiplier = targetExpr.Coefficients[sourceExpr.TargetVar];
                            targetExpr.Coefficients.Remove(sourceExpr.TargetVar);
                            targetExpr.FreeTerm += multiplier * sourceExpr.FreeTerm;

                            foreach (var kvp in sourceExpr.Coefficients)
                            {
                                if (targetExpr.Coefficients.ContainsKey(kvp.Key))
                                    targetExpr.Coefficients[kvp.Key] += multiplier * kvp.Value;
                                else
                                    targetExpr.Coefficients[kvp.Key] = multiplier * kvp.Value;

                                if (Math.Abs(targetExpr.Coefficients[kvp.Key]) < 1e-9)
                                    targetExpr.Coefficients.Remove(kvp.Key);
                            }
                        }
                    }
                }

                Log("\nОстаточні вирази для вільних двоїстих змінних (через незалежні змінні):");
                foreach (var expr in eliminatedExpressions)
                {
                    string exprStr = $"{expr.TargetVar} = ";
                    List<string> terms = new List<string>();

                    var keys = expr.Coefficients.Keys.OrderBy(k => k).ToList();
                    foreach (var key in keys)
                    {
                        terms.Add($"{expr.Coefficients[key]:F2} * {key}");
                    }
                    terms.Add($"{expr.FreeTerm:F2}");

                    string finalStr = exprStr + string.Join(" + ", terms).Replace("+ -", "- ");
                    Log(finalStr);
                }
            }

            Log("Пошук опорного розв'язку:");
            while (true)
            {
                int colsCount = table.GetLength(1) - 1;
                int r = -1;
                for (int i = 0; i < m; i++)
                {
                    if (table[i, colsCount] < -1e-9) { r = i; break; }
                }

                if (r == -1)
                {
                    Log("\nЗнайдено опорний розв'язок:");
                    break;
                }

                int s = -1;
                for (int j = 0; j < colsCount; j++)
                {
                    if (table[r, j] < -1e-9) { s = j; break; }
                }

                if (s == -1)
                {
                    Log("Система обмежень є суперечливою.");
                    PromptSaveToFile();
                    return;
                }

                int pivotRow = -1;
                double minRatio = double.MaxValue;
                for (int i = 0; i < m; i++)
                {
                    // ВИПРАВЛЕНО 3: Для знаходження опорного розв'язку елементи стовпця мають бути від'ємними
                    if (table[i, s] < -1e-9)
                    {
                        double ratio = table[i, colsCount] / table[i, s];
                        if (ratio >= 0 && ratio < minRatio)
                        {
                            minRatio = ratio;
                            pivotRow = i;
                        }
                    }
                }

                if (pivotRow == -1) pivotRow = r;

                Log($"Розв'язувальний рядок: {rowHeaders[pivotRow].Split(' ').Last()}");
                Log($"Розв'язувальний стовпець: {colHeaders[s].Split(',').Last().Trim()}");

                table = DoSimplexMJEStep(table, pivotRow, s);
                SwapLPPHeaders(ref rowHeaders[pivotRow], ref colHeaders[s]);
                LogMatrixLPP(table, rowHeaders, colHeaders);
            }

            Log("Розвʼязки прямої задачі:");
            PrintSolutionX(table, rowHeaders, originalN);
            Log("Розвʼязки двоїстої задачі:");
            PrintDualSolutions(table, rowHeaders, colHeaders, originalN, originalM, eliminatedExpressions);

            Log("\nПошук оптимального розв'язку:");
            while (true)
            {
                int colsCount = table.GetLength(1) - 1;
                int s = -1;
                for (int j = 0; j < colsCount; j++)
                {
                    if (table[m, j] < -1e-9) { s = j; break; }
                }

                if (s == -1)
                {
                    Log("\nЗнайдено оптимальний розв'язок:");
                    break;
                }

                int pivotRow = -1;
                double minRatio = double.MaxValue;
                for (int i = 0; i < m; i++)
                {
                    if (table[i, s] > 1e-9)
                    {
                        double ratio = table[i, colsCount] / table[i, s];
                        if (ratio >= 0 && ratio < minRatio)
                        {
                            minRatio = ratio;
                            pivotRow = i;
                        }
                    }
                }

                if (pivotRow == -1)
                {
                    Log("Функція мети не обмежена зверху.");
                    PromptSaveToFile();
                    return;
                }

                Log($"Розв'язувальний рядок: {rowHeaders[pivotRow].Split(' ').Last()}");
                Log($"Розв'язувальний стовпець: {colHeaders[s].Split(',').Last().Trim()}");

                table = DoSimplexMJEStep(table, pivotRow, s);
                SwapLPPHeaders(ref rowHeaders[pivotRow], ref colHeaders[s]);
                LogMatrixLPP(table, rowHeaders, colHeaders);
            }

            Log("Розвʼязки прямої задачі:");
            PrintSolutionX(table, rowHeaders, originalN);
            Log("Розвʼязки двоїстої задачі:");
            PrintDualSolutions(table, rowHeaders, colHeaders, originalN, originalM, eliminatedExpressions);

            int finalCols = table.GetLength(1) - 1;
            double finalZ = isMax ? table[m, finalCols] : -table[m, finalCols];
            Log($"Max (Z) = {finalZ:F2}");
            Log($"Min (W) = {finalZ:F2}");

            PromptSaveToFile();
        }
    }
}