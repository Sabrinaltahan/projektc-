using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Threading.Tasks;

namespace UserDepartmentPredictionApp
{
    public partial class Form1 : Form
    {
        private ITransformer trainedModel;
        private PredictionEngine<ModelInput, ModelOutput> predictionEngine;

        public Form1()
        {
            InitializeComponent();

            // Add departments to dropdown list
            comboBoxDepartment.Items.Add("HR");
            comboBoxDepartment.Items.Add("IT");
            comboBoxDepartment.Items.Add("Sales");
            comboBoxDepartment.Items.Add("Finance");
            comboBoxDepartment.Items.Add("Operations");

            // Initialize and ensure the database is created
            using (var context = new AppDbContext())
            {
                context.Database.EnsureCreated();
            }

            // Display all users on load
            ShowAllUsers();
        }

        private (ITransformer model, DataViewSchema schema, MulticlassClassificationMetrics metrics) BuildAndTrainModel(string datasetPath)
        {
            var mlContext = new MLContext();

            // Load data
            IDataView dataView = mlContext.Data.LoadFromTextFile<ModelInput>(
                datasetPath, hasHeader: true, separatorChar: '\t');

            // Split data into training and test sets
            var trainTestSplit = mlContext.Data.TrainTestSplit(dataView, testFraction: 0.2);
            var trainData = trainTestSplit.TrainSet;
            var testData = trainTestSplit.TestSet;

            // Define the training pipeline
            var pipeline = mlContext.Transforms.Text.FeaturizeText("Features", nameof(ModelInput.Description))
                .Append(mlContext.Transforms.Conversion.MapValueToKey("Label", nameof(ModelInput.Department)))
                .Append(mlContext.Transforms.Concatenate("Features", "Features"))
                .Append(mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy("Label", "Features"))
                .Append(mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            // Train the model
            var model = pipeline.Fit(trainData);

            // Evaluate the model on the test data
            var predictions = model.Transform(testData);
            var metrics = mlContext.MulticlassClassification.Evaluate(predictions);

            // Print evaluation metrics to the console
            Console.WriteLine($"MacroAccuracy: {metrics.MacroAccuracy:F2}");
            Console.WriteLine($"MicroAccuracy: {metrics.MicroAccuracy:F2}");
            Console.WriteLine($"LogLoss: {metrics.LogLoss:F2}");

            return (model, dataView.Schema, metrics);
        }


        private void SaveModel(ITransformer model, DataViewSchema schema, string modelPath)
        {
            var mlContext = new MLContext();
            using (var fileStream = new FileStream(modelPath, FileMode.Create, FileAccess.Write, FileShare.Write))
            {
                mlContext.Model.Save(model, schema, fileStream);
            }
        }

        private PredictionEngine<ModelInput, ModelOutput> LoadModel(string modelPath)
        {
            var mlContext = new MLContext();
            ITransformer trainedModel;

            using (var stream = new FileStream(modelPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                trainedModel = mlContext.Model.Load(stream, out _);
            }

            return mlContext.Model.CreatePredictionEngine<ModelInput, ModelOutput>(trainedModel);
        }

        private string PredictDepartment(string description)
        {
            var input = new ModelInput { Description = description };
            var prediction = predictionEngine.Predict(input);
            return prediction.PredictedDepartment;
        }


        // Existing Add Button now only adds user to DB
        private void BtnAdd_Click(object sender, EventArgs e)
        {
            string name = txtName.Text;
            string email = txtEmail.Text;
            string ageText = txtAge.Text;
            string description = txtDescription.Text;

            // Validate input fields
            if (string.IsNullOrWhiteSpace(name) || 
                string.IsNullOrWhiteSpace(email) || 
                string.IsNullOrWhiteSpace(ageText) || 
                string.IsNullOrWhiteSpace(description))
            {
                MessageBox.Show("All fields must be filled out.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!int.TryParse(ageText, out int age))
            {
                MessageBox.Show("Age must be a valid number.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Check if the user already exists by email
            using (var context = new AppDbContext())
            {
                if (context.Users.Any(u => u.Email == email))
                {
                    MessageBox.Show("A user with this email already exists.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Predict department
                string predictedDepartment = PredictDepartment(description);

                // Add new user to the database
                var newUser = new User
                {
                    Name = name,
                    Email = email,
                    Age = age,
                    Description = description,
                    PredictedDepartment = predictedDepartment,
                    Department = comboBoxDepartment.SelectedItem?.ToString()
                };

                context.Users.Add(newUser);
                context.SaveChanges();
            }

            // Refresh grid and clear fields
            ShowAllUsers();
            ClearFields();
        }


        // New Button to Display All Users in TextBox
        private void BtnShowAllUsers_Click(object sender, EventArgs e)
        {
            ShowAllUsers();
        }

        private void ShowAllUsers()
        {
            using (var context = new AppDbContext())
            {
                var allUsers = context.Users.ToList();

                // Bind the data to the DataGridView
                dataGridViewUsers.DataSource = allUsers;

                // Automatically adjust column widths based on the content
                dataGridViewUsers.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            }
        }


        // Sort Users by Age and Display in TextBox
        private void BtnSort_Click(object sender, EventArgs e)
        {
            SortUsersByAge();
        }

        //private void SortUsersByAge()
        //{
        //    using (var context = new AppDbContext())
        //    {
        //        var sortedUsers = context.Users.OrderBy(u => u.Age).ToList();
        //        textBoxUserDisplay.Clear();
        //        foreach (var user in sortedUsers)
        //        {
        //            textBoxUserDisplay.AppendText($"Name: {user.Name}, Email: {user.Email}, Age: {user.Age}, Dept: {user.Department}\n");
        //        }
        //    }
        //}

        private void SortUsersByAge()
        {
            using (var context = new AppDbContext())
            {
                var usersList = context.Users.ToList();

                // Bubble Sort to sort users by age
                for (int i = 0; i < usersList.Count - 1; i++)
                {
                    for (int j = 0; j < usersList.Count - i - 1; j++)
                    {
                        if (usersList[j].Age > usersList[j + 1].Age)
                        {
                            var temp = usersList[j];
                            usersList[j] = usersList[j + 1];
                            usersList[j + 1] = temp;
                        }
                    }
                }

                // Bind the sorted data to the DataGridView
                dataGridViewUsers.DataSource = usersList;

                // Automatically adjust column widths
                dataGridViewUsers.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            }
        }



    

        private void BtnPredict_Click_1(object sender, EventArgs e)
        {
            string description = txtDescription.Text;
            string predictedDepartment = PredictDepartment(description);

            lblPredictedDepartment.Text = predictedDepartment;
        }

        private void DataGridViewUsers_SelectionChanged(object sender, EventArgs e)
        {
            if (dataGridViewUsers.SelectedRows.Count > 0)
            {
                var selectedRow = dataGridViewUsers.SelectedRows[0];
                var selectedUser = (User)selectedRow.DataBoundItem;

                txtName.Text = selectedUser.Name;
                txtEmail.Text = selectedUser.Email;
                txtAge.Text = selectedUser.Age.ToString();
                txtDescription.Text = selectedUser.Description;
                comboBoxDepartment.SelectedItem = selectedUser.Department;
                lblPredictedDepartment.Text = selectedUser.PredictedDepartment;
            }
        }

        private void BtnUpdate_Click(object sender, EventArgs e)
        {
            if (dataGridViewUsers.SelectedRows.Count > 0)
            {
                var selectedRow = dataGridViewUsers.SelectedRows[0];
                var selectedUser = (User)selectedRow.DataBoundItem;

                // Update user information
                selectedUser.Name = txtName.Text;
                selectedUser.Email = txtEmail.Text;
                selectedUser.Age = int.Parse(txtAge.Text);
                selectedUser.Description = txtDescription.Text;
                selectedUser.Department = comboBoxDepartment.SelectedItem?.ToString();

                using (var context = new AppDbContext())
                {
                    context.Users.Update(selectedUser);
                    context.SaveChanges();
                }

                // Refresh grid and clear fields
                ShowAllUsers();
                ClearFields();
            }
        }

        private void BtnRemove_Click(object sender, EventArgs e)
        {
            if (dataGridViewUsers.SelectedRows.Count > 0)
            {
                var selectedRow = dataGridViewUsers.SelectedRows[0];
                var selectedUser = (User)selectedRow.DataBoundItem;

                // Confirm deletion
                DialogResult result = MessageBox.Show($"Are you sure you want to remove {selectedUser.Name}?", "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (result == DialogResult.Yes)
                {
                    using (var context = new AppDbContext())
                    {
                        context.Users.Remove(selectedUser);
                        context.SaveChanges();
                    }

                    // Refresh grid and clear fields
                    ShowAllUsers();
                    ClearFields();
                }
            }
        }

        private void ClearFields()
        {
            txtName.Clear();
            txtEmail.Clear();
            txtAge.Clear();
            txtDescription.Clear();
            comboBoxDepartment.SelectedIndex = -1;
            lblPredictedDepartment.Text = "";
        }

        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.InitialDirectory = "c:\\";
                openFileDialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string filePath = openFileDialog.FileName;
                    LoadUserDataFromFile(filePath);
                }
            }
        }

        private void LoadUserDataFromFile(string filePath)
        {
            try
            {
                // Read the file line by line
                var lines = File.ReadAllLines(filePath);
                if (lines.Length > 0)
                {
                    // Assuming the first line contains user data
                    var userData = lines[0].Split('\t'); // Split by tab character

                    if (userData.Length == 4)
                    {
                        txtName.Text = userData[0];
                        txtEmail.Text = userData[1];
                        txtAge.Text = userData[2];
                        txtDescription.Text = userData[3];
                    }
                    else
                    {
                        MessageBox.Show("Invalid user data format. Please ensure it has four tab-separated values.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    MessageBox.Show("The file is empty.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                           }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while reading the file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

   

        private void BtnClear_Click(object sender, EventArgs e)
        {
            ClearFields();
            dataGridViewUsers.DataSource = null;  // Clear the grid
        }

        private async void ModelBtn_Click(object sender, EventArgs e)
        {
            try
            {
                // Show loading indicator by changing the button text to Loading...
                modelBtn.Text = "Loading...";
                modelBtn.Enabled = false;

                // Build, train, and save the model asynchronously
                string datasetPath = "department_data.tsv";
                var result = await Task.Run(() => BuildAndTrainModel(datasetPath));
                trainedModel = result.model;
                string modelPath = "department_model.zip";

                // Save the trained model
                await Task.Run(() => SaveModel(trainedModel, result.schema, modelPath));

                // Load the model for making predictions
                predictionEngine = LoadModel(modelPath);

                // Format metrics for display in the MessageBox
                string metricsMessage = $"Model built and trained successfully!\n\n" +
                                        $"Class-Balanced Accuracy (Macro): {result.metrics.MacroAccuracy:F2}\n" +  // bigger better
                                        $"Overall Accuracy (Micro): {result.metrics.MicroAccuracy:F2}\n" +        // bigger better
                                        $"Log Loss: {result.metrics.LogLoss:F2}";      // smaller better

                // Show metrics in MessageBox
                MessageBox.Show(metricsMessage, "Model Training Results", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // Hide the loading indicator and re-enable the button
                modelBtn.Text = "Build Model";
                modelBtn.Enabled = true;
            }
        }

        private void Button1_Click(object sender, EventArgs e)
        {

        }
    }
}
