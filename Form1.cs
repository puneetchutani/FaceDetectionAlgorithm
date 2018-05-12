using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Dynamsoft.UVC;
using Dynamsoft.Core;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Common.Contract;
using Microsoft.ProjectOxford.Face.Contract;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Data.SqlClient;

namespace FaceDetectionApp
{
    public partial class Form1 : Form
    {
        private readonly IFaceServiceClient faceServiceClient = new FaceServiceClient("be0e0b4c68414c3b8704092ee4fb96a2", "https://westcentralus.api.cognitive.microsoft.com/face/v1.0");
        private CameraManager m_CameraManager = null;
        private Camera m_CurrentCamera = null;
        string path = string.Empty;
        private static int counter = 0;
        private ImageCore m_ImageCore = null;
        public Form1()
        {
            InitializeComponent();
            path = Path.Combine(Environment.CurrentDirectory, "img.jpeg");
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;
            InitialiseAndStartWebCam();
            startTimer();
            Cursor = Cursors.Arrow;
        }

        private void InitialiseAndStartWebCam()
        {
            //Initialise the webcamera and start
            m_CameraManager = new CameraManager("t0068MgAAALy2+1uzMH3do0eStkxwYPt8igdrUGRffaUDbP8qgfsa+K8waVxspoiiFvfyFNcuWorQR1uXAoOCvPJ3eKcLELE=");
            m_ImageCore = new ImageCore();
            dsViewer1.Bind(m_ImageCore);
            if (m_CameraManager.GetCameraNames() != null)
            {
                m_CurrentCamera = m_CameraManager.SelectCamera(0);
                m_CurrentCamera.SetVideoContainer(pictureBox1.Handle);
                m_CurrentCamera.Open();
            }
        }

        private void startTimer()
        {
            timer1 = new Timer();
            timer1.Start();
            timer1.Interval = 1000;
            timer1.Tick += Timer1_Tick;
        }

        private async void Timer1_Tick(object sender, EventArgs e)
        {
            button1.Text = "Image will capture in " + (10 - counter).ToString() + " seconds";
            counter++;
            if (counter % 10 == 0)
            {
                counter = 0;
                timer1.Stop();
                button1.Text = "Capturing...";
                var key = await GrabImageAndStoreEmotions();
                button1.Text = "Captured!";
                //label1.Text = "";
                SaveImageAndEmotions(key);
                startTimer();
            }
        }

        private async Task<string> GrabImageAndStoreEmotions()
        {
            string key = string.Empty;
            try
            {
                var currentImage = m_CurrentCamera.GrabImage();
                Bitmap bit =new Bitmap(currentImage);

                using (MemoryStream ms = new MemoryStream())
                {
                    // Convert Image to byte[]
                    bit.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                    //dpgraphic.image.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                    byte[] imageBytes = ms.ToArray();
                    var faces = await UploadAndDetectFaces(ms);
                    if (faces.Any())
                    {
                        var face = faces[0];
                        var emotionScores = face.FaceAttributes.Emotion;
                        key = emotionScores.ToRankedList().First().Key;
                    }
                }
                //currentImage.Dispose();
                //currentImage = null;

                //string outputFileName = "...";
                //using (MemoryStream memory = new MemoryStream())
                //{
                //    using (FileStream fs = new FileStream(outputFileName, FileMode.Create, FileAccess.ReadWrite))
                //    {
                //        currentImage.Save(memory, ImageFormat.Jpeg);
                //        byte[] bytes = memory.ToArray();
                //        fs.Write(bytes, 0, bytes.Length);
                //    }
                //}
                //bit.Save(path);

                m_ImageCore.IO.LoadImage(bit);
                label1.Text = "The emotion is: " + key;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Exception: " + ex.Message);
                return null;
            }
            return key;
        }

        private void SaveImageAndEmotions(string key)
        {
            try
            {
                //Read Image Bytes into a byte array
                byte[] imageData = ReadFile(path);

                //Initialize SQL Server Connection
                SqlConnection con = new SqlConnection("Data Source=LISA;Initial Catalog=PIAEData;Integrated Security=True");
                con.Open();
                string qry = "insert into ImageStore (ImageStore, Emotions) values(@ImageStore, @Emotions)";
                SqlCommand SqlCom = new SqlCommand(qry, con);

                //We are passing Original Image Path and 
                //Image byte data as SQL parameters.
                SqlCom.Parameters.Add(new SqlParameter("@ImageStore", (object)imageData));
                SqlCom.Parameters.Add(new SqlParameter("@Emotions", (object)key));

                //Open connection and execute insert query.
                SqlCom.ExecuteNonQuery();
                con.Close();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error");
            }
        }

        byte[] ReadFile(string sPath)
        {
            byte[] data = null;
            FileInfo fInfo = new FileInfo(sPath);
            long numBytes = fInfo.Length;
            FileStream fStream = new FileStream(sPath, FileMode.Open, FileAccess.Read);
            BinaryReader br = new BinaryReader(fStream);
            data = br.ReadBytes((int)numBytes);
            return data;
        }

        private async Task<Face[]> UploadAndDetectFaces(Stream imageFileStream)
        {
            IEnumerable<FaceAttributeType> faceAttributes = new FaceAttributeType[] { FaceAttributeType.Gender, FaceAttributeType.Age, FaceAttributeType.Smile, FaceAttributeType.Emotion, FaceAttributeType.Glasses };
            // Call the Face API.
            try
            {
                Face[] faces = await faceServiceClient.DetectAsync(imageFileStream, returnFaceId: true, returnFaceLandmarks: false, returnFaceAttributes: faceAttributes);
                return faces;
            }
            catch (FaceAPIException f)
            {
                MessageBox.Show(f.ErrorMessage, f.ErrorCode);
                return new Face[0];
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Error");
                return new Face[0];
            }
        }

        //private async Task<Face[]> UploadAndDetectFaces(string imageFilePath)
        //{
        //    IEnumerable<FaceAttributeType> faceAttributes = new FaceAttributeType[] { FaceAttributeType.Gender, FaceAttributeType.Age, FaceAttributeType.Smile, FaceAttributeType.Emotion, FaceAttributeType.Glasses };
        //    // Call the Face API.
        //    try
        //    {
        //        using (Stream imageFileStream = File.OpenRead(imageFilePath))
        //        {
        //            Face[] faces = await faceServiceClient.DetectAsync(imageFileStream, returnFaceId: true, returnFaceLandmarks: false, returnFaceAttributes: faceAttributes);
        //            return faces;
        //        }
        //    }
        //    catch (FaceAPIException f)
        //    {
        //        MessageBox.Show(f.ErrorMessage, f.ErrorCode);
        //        return new Face[0];
        //    }
        //    catch (Exception e)
        //    {
        //        MessageBox.Show(e.Message, "Error");
        //        return new Face[0];
        //    }
        //}

        //private async void button3_Click(object sender, EventArgs e)
        //{
        //    var faces = await UploadAndDetectFaces(path);
        //    if (faces.Any())
        //    {
        //        var face = faces[0];
        //        var emotionScores = face.FaceAttributes.Emotion;
        //        var rank = emotionScores.ToRankedList().First();
        //        var key = rank.Key;
        //        var value = rank.Value*100;
        //    }
        //}

        private void button2_Click(object sender, EventArgs e)
        {
            timer1.Stop();
            button1.Text = "Start capturing image";
            //TODO analyse the emotions thats is maximum from the database 
        }
    }
}
