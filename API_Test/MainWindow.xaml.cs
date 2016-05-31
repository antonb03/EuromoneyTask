using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Configuration;
using System.Data.SqlClient;
using System.Data;
using System.Net;
using Newtonsoft.Json.Linq;
using System.Globalization;

namespace API_Test
{
    public partial class MainWindow : Window
    {
        string connectionString;
        SqlConnection connection;
        DataTable capitalTable;
        string json;

        public MainWindow()
        {
            InitializeComponent();
            connectionString = "Data Source=(LocalDB)\\MSSQLLocalDB;AttachDbFilename=|DataDirectory|\\WeatherDB.mdf;Integrated Security=True";

            using (connection = new SqlConnection(connectionString))
            using (SqlDataAdapter adapter = new SqlDataAdapter("SELECT Capital FROM Countries", connection)) {
                connection.Open();
                capitalTable = new DataTable();
                
                adapter.Fill(capitalTable);
                
            }

            //clean table from previous launch
            using (connection = new SqlConnection(connectionString))
            using (SqlCommand command = new SqlCommand("DELETE WeatherData", connection)) {
                connection.Open();
                command.ExecuteNonQuery();
            }

            //fill WeatherData table with weather info
            foreach (DataRow row in capitalTable.Rows) {
                json = new WebClient().DownloadString("http://api.openweathermap.org/data/2.5/weather?q="+ row["Capital"].ToString() + "&appid=ff474c733cc62b14041df17b6d2ba1ca");
                var obj = JObject.Parse(json);
                 
                double temp = (double)obj.SelectToken("main.temp")-273.15; //convert to Celcium
                int pressure = (int)obj.SelectToken("main.pressure");
                int humidity = (int)obj.SelectToken("main.humidity");
                double temp_min = (double)obj.SelectToken("main.temp_min")-273.15;
                double temp_max = (double)obj.SelectToken("main.temp_max")-273.15;
                double wind_speed = (double)obj.SelectToken("wind.speed");
                int wind_deg = 0;
                if (obj.SelectToken("wind.deg") != null)
                {
                    wind_deg = (int)obj.SelectToken("wind.deg");
                }
                string windDirection = convertWindDirection(wind_deg, wind_speed);// convert degree into letters
                int clouds = (int)obj.SelectToken("clouds.all");
                int rain=0;
                if (obj.SelectToken("rain.3h") != null)
                {
                    rain = (int)obj.SelectToken("rain.3h");
                }
                string cityName = (string)obj.SelectToken("name");
                string sunrise = FromUnixTime((long)obj.SelectToken("sys.sunrise"));
                string sunset = FromUnixTime((long)obj.SelectToken("sys.sunset"));




                string query = "INSERT INTO WeatherData VALUES(@temp, @pressure, @humidity, @temp_min, @temp_max, @wind_speed, @wind_dir, @clouds, @rain, @cityName, @sunrise, @sunset)";

                using (connection = new SqlConnection(connectionString))
                using (SqlCommand command = new SqlCommand(query, connection)) {

                    command.Parameters.AddWithValue("@temp", temp);
                    command.Parameters.AddWithValue("@pressure", pressure);
                    command.Parameters.AddWithValue("@humidity", humidity);
                    command.Parameters.AddWithValue("@temp_min", temp_min);
                    command.Parameters.AddWithValue("@temp_max", temp_max);
                    command.Parameters.AddWithValue("@wind_speed", wind_speed);
                    command.Parameters.AddWithValue("@wind_dir", windDirection);
                    command.Parameters.AddWithValue("@clouds", clouds);
                    command.Parameters.AddWithValue("@rain", rain);
                    command.Parameters.AddWithValue("@cityName", cityName);
                    command.Parameters.AddWithValue("@sunrise", sunrise);
                    command.Parameters.AddWithValue("@sunset", sunset);
                    connection.Open();
                    command.ExecuteNonQuery();
                }
            }

            //create intial result and display it to the user
            using (connection = new SqlConnection(connectionString))
            using (SqlDataAdapter adapter = new SqlDataAdapter("SELECT CityName as 'City Name', Temp as 'Temperature(C)', Clouds as 'Clouds %', Rain as 'Rain mm', Humidity as 'Humidity %', Pressure as 'Pressure hPa', Wind_speed as 'Wind Speed', Wind_deg as 'Wind Direction', Temp_min, Temp_max FROM WeatherData", connection))

            {
                DataTable weather = new DataTable();
                adapter.Fill(weather);
                dataGrid.DataContext = weather.DefaultView;

            } 
        }

        private void filterButton_Click(object sender, RoutedEventArgs e)
        {
            if (checkTextBox())
            {
                //filter results based on user input
                string query = createQueryFromTextBox();
                using (connection = new SqlConnection(connectionString))
                using (SqlDataAdapter command = new SqlDataAdapter(query, connection)) //"SELECT CityName, Temp FROM WeatherData WHERE Temp = @temp"
                {
                    connection.Open();
                    DataTable searchTable = new DataTable();
                    command.SelectCommand.Parameters.AddWithValue("@temp", tempTextBox.Text);
                    command.Fill(searchTable);
                    dataGrid.DataContext = searchTable.DefaultView;

                }
            }
        }

        private void tempTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private string convertWindDirection(int wind_deg, double wind_speed) {
            //convert degree into letter direction
            if (wind_speed == 0) {
                return "No Wind";
            }
            int val = Convert.ToInt32(wind_deg / 22.5 + 0.5);
            string [] directions = new string [] { "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW" };
            return directions[val % 16];

            
        }

        public string FromUnixTime(long unixTime)
        {
            //convert time 
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime dt =  epoch.AddSeconds(unixTime);
            return dt.ToString("HH:mm");
        }

        public string createQueryFromTextBox()
        {
            //creates query based on the user input
            string selectStatement = "SELECT CityName as 'City Name', Temp as 'Temperature(C)', Clouds as 'Clouds %', Rain as 'Rain mm', Humidity as 'Humidity %', Pressure as 'Pressure hPa', Wind_speed as 'Wind Speed', Wind_deg as 'Wind Direction', Temp_min, Temp_max FROM WeatherData ";
            string whereStatement = "";

            if (tempTextBox.Text.Length > 0)
            {

                if (whereStatement.Length == 0)
                {
                    whereStatement += "WHERE ";
                    whereStatement += "Temp = " + tempTextBox.Text + " ";
                }
                else
                {
                    whereStatement += "Temp = " + tempTextBox.Text + " ";
                }
            }
            
            if (pressureTextBox.Text.Length > 0)
            {
                if (whereStatement.Length == 0)
                {
                    whereStatement += "WHERE ";
                    whereStatement += "Pressure = " + pressureTextBox.Text + " ";
                }
                else
                {
                    whereStatement += "AND Pressure = " + pressureTextBox.Text + " ";
                }
            }

            if (humidityTextBox.Text.Length > 0)
            {
                if (whereStatement.Length == 0)
                {
                    whereStatement += "WHERE ";
                    whereStatement += "Humidity = " + humidityTextBox.Text + " ";
                }
                else
                {
                    whereStatement += "AND Humidity = " + humidityTextBox.Text + " ";
                }
            }

            if (tempMinTextBox.Text.Length > 0)
            {
                if (whereStatement.Length == 0)
                {
                    whereStatement += "WHERE ";
                    whereStatement += "Temp_min = " + tempMinTextBox.Text + " ";
                }
                else
                {
                    whereStatement += "AND Temp_min = " + tempMinTextBox.Text + " ";
                }
            }

            if (tempMaxTextBox.Text.Length > 0)
            {
                if (whereStatement.Length == 0)
                {
                    whereStatement += "WHERE ";
                    whereStatement += "Temp_max = " + tempMaxTextBox.Text + " ";
                }
                else
                {
                    whereStatement += "AND Temp_max = " + tempMaxTextBox.Text + " ";
                }
            }

            if (windSpeedTextBox.Text.Length > 0)
            {
                if (whereStatement.Length == 0)
                {
                    whereStatement += "WHERE ";
                    whereStatement += "Wind_speed = " + windSpeedTextBox.Text + " ";
                }
                else
                {
                    whereStatement += "AND Wind_speed = " + windSpeedTextBox.Text + " ";
                }
            }

            if (windDirectionTextBox.Text.Length > 0)
            {
                if (whereStatement.Length == 0)
                {
                    whereStatement += "WHERE ";
                    whereStatement += "Wind_deg = '" + windDirectionTextBox.Text + "' ";
                }
                else
                {
                    whereStatement += "AND Wind_deg = '" + windDirectionTextBox.Text + "' ";
                }
            }

            if (cloudsTextBox.Text.Length > 0)
            {
                if (whereStatement.Length == 0)
                {
                    whereStatement += "WHERE ";
                    whereStatement += "Clouds = " + cloudsTextBox.Text + " ";
                }
                else
                {
                    whereStatement += "AND Clouds = " + cloudsTextBox.Text + " ";
                }
            }

            if (rainTextBox.Text.Length > 0)
            {
                if (whereStatement.Length == 0)
                {
                    whereStatement += "WHERE ";
                    whereStatement += "Rain = " + rainTextBox.Text + " ";
                }
                else
                {
                    whereStatement += "AND Rain = " + rainTextBox.Text + " ";
                }
            }

            if (cityNameTextBox.Text.Length > 0)
            {
                if (whereStatement.Length == 0)
                {
                    whereStatement += "WHERE ";
                    whereStatement += "CityName = '" + cityNameTextBox.Text + "' ";
                }
                else
                {
                    whereStatement += "AND CityName = '" + cityNameTextBox.Text + "' ";
                }
            }
            

            string query = selectStatement + whereStatement;

            return query;
        }

        public bool checkTextBox() {
            //checks text boxes for corrrect input
            
            if (tempTextBox.Text.Length != 0)
            {
                double num;
                if (double.TryParse(tempTextBox.Text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out num))
                {
                    //it is a number
                }
                else {
                    MessageBox.Show("Wrong input for Temperature");
                    return false;
                }
            }
            if (pressureTextBox.Text.Length != 0) {
                double num;
                if (double.TryParse(pressureTextBox.Text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out num))
                {

                }
                else
                {
                    MessageBox.Show("Wrong input for Pressure");
                    return false;
                }
            }
            if (humidityTextBox.Text.Length != 0)
            {
                double num;
                if (double.TryParse(humidityTextBox.Text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out num)){}
                else{
                    MessageBox.Show("Wrong input for Humidity");
                    return false;
                }
            }
            if (tempMinTextBox.Text.Length != 0)
            {
                double num;
                if (double.TryParse(tempMinTextBox.Text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out num)) { }
                else {
                    MessageBox.Show("Wrong input for Temp_min");
                    return false;
                }
            }
            if (tempMaxTextBox.Text.Length != 0)
            {
                double num;
                if (double.TryParse(tempMaxTextBox.Text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out num)) { }
                else {
                    MessageBox.Show("Wrong input for Temp_max");
                    return false;
                }
            }
            if (windSpeedTextBox.Text.Length != 0)
            {
                double num;
                if (double.TryParse(windSpeedTextBox.Text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out num)) { }
                else {
                    MessageBox.Show("Wrong input for Wind Speed");
                    return false;
                }
            }
            if (cloudsTextBox.Text.Length != 0)
            {
                double num;
                if (double.TryParse(cloudsTextBox.Text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out num)) { }
                else {
                    MessageBox.Show("Wrong input for Clouds");
                    return false;
                }
            }
            if (rainTextBox.Text.Length != 0)
            {
                double num;
                if (double.TryParse(rainTextBox.Text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out num)) { }
                else {
                    MessageBox.Show("Wrong input for Rain");
                    return false;
                }
            }
            if (windDirectionTextBox.Text.Length != 0)
            {
                string[] directions = new string[] { "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW" };
                string searchString = windDirectionTextBox.Text.ToUpper();
                int pos = Array.IndexOf(directions, searchString);
                if (pos == -1) {
                    MessageBox.Show("Wrong input for Wind Direction");
                    return false;
                }

            }


            return true;
        }





    }

}
