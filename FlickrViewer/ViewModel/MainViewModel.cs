using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Xml.Linq;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using FlickrViewer.Model;
using FlickrViewer.View;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Forms;

namespace FlickrViewer.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        // Use your Flickr API key here--you can get one at:
        // http://www.flickr.com/services/apps/create/apply
        private const string KEY = "a445298c67eb831e4037857224f56822";      // secret = ad0a14caf46aae49

        // Create an WebClient object called flickrClient to invoke Flickr web service 
        private WebClient _flickrClient;

        //Initialize a Task<String> object called flickrTask to null - Task<string> that queries Flickr
        private Task<String> _flickrTask = null;     // Task<string> that queries Flickr

        private ObservableCollection<FlickrResult> _imagesList;
        public ObservableCollection<FlickrResult> ImagesList
        {
            get
            {
                return _imagesList;
            }
            set
            {
                Set(() => this.ImagesList, ref _imagesList, value);
            }
        }

        private FlickrResult _selectedImage;
        public FlickrResult SelectedImage
        {
            get
            {
                return _selectedImage;
            }
            set
            {
                Set(() => SelectedImage, ref _selectedImage, value);
                ImagesListBox_SelectedIndexChanged(this, EventArgs.Empty);
            }
        }

        private string _enteredTag;
        public string EnteredTag
        {
            get
            {
                return _enteredTag;
            }
            set
            {
                Set(() => this.EnteredTag, ref _enteredTag, value);
            }
        }

        private Image _displayedImage;
        public Image DisplayedImage
        {
            get
            {
                return _displayedImage;
            }
            set
            {
                Set(() => this.DisplayedImage, ref _displayedImage, value);
            }
        }

        public ICommand SearchCommand { get; private set; }

        public MainViewModel()
        {
            ImagesList = new ObservableCollection<FlickrResult>();
            DisplayedImage = new Image();
            EnteredTag = String.Empty;
            _flickrClient = new WebClient();
            SearchCommand = new RelayCommand(() => SearchButton_Click(this, EventArgs.Empty));
        } // end constructor

        // The method searchButton_Click initiates the asynchronous Flickr search query; 
        // display results when query completes
        private async void SearchButton_Click(object sender, EventArgs e)
        {
            // Check whether user started a search previously (i.e., flickrTask is not null and the prior search has not completed) and,
            // if so, whether that search is already completed. 
            if (_flickrTask != null &&
                _flickrTask.Status != TaskStatus.RanToCompletion)
            {
                //If an existing search is still being performed, the program displays a dialog asking if user wish  to cancel the search. 
                var result = MessageBox.Show(
                   "Cancel the current Flickr search?",
                   "Are you sure?", MessageBoxButtons.YesNo,
                   MessageBoxIcon.Question);

                // If user clicks No, the event handler simply returns. Otherwise, the program calls  the WebClient's  CancelAsync 
                //method to terminiate the search.
                if (result == DialogResult.No)
                    return;
                else
                    _flickrClient.CancelAsync(); // cancel current search
            } // end if

            // Create a URL for invoking the Flickr web service's method flickr.photos.search.
            // with Key you get from the Flickr website, tag, tag_mode=all, per_page = 500 and 
            // privacy_filter=1. Use the inputTextBox.Text.Replace(" ", ",") for tag.

            var flickrURL = string.Format("https://api.flickr.com/services/rest/?method=flickr.photos.search&api_key={0}&tags={1}&tag_mode=all&privacy_filter=1", KEY, EnteredTag.Replace(" ", ","));


            ImagesList.Clear();                          // clear imagesListBox
            DisplayedImage.Source = null;                // clear pictureBox
            ImagesList.Add(new FlickrResult { Title = "Loading..." });   // display Loading...

            try
            {
                // Call WebClient's DownloadStringTaskAsync method using the flickrURL specified as the method's string argument to request information from
                // the server. Assign the task returned from the method to flickrTask.
                _flickrTask =
                    _flickrClient.DownloadStringTaskAsync(flickrURL);

                // await fickrTask then parse results with XDocument
                XDocument xmlResponse = XDocument.Parse(await _flickrTask);

                // Gather  from each photo element in the XML the id, title, secret, server, and farm attributes, then create an object class FlickResult using LINQ.
                // Each FlickrResult contains:
                //      A Title property - initialized with the photo element's title attribute
                //      A URL property - assembled fromt the photo element's id, secret, server, and farm (a farm is a collection of servers on the Internet) attributes.
                //      The format of the URL for each image is specified at http://www.flickr.com/services/api/misc.urls.html
                var flickrPhotos =
                   from photo in xmlResponse.Descendants("photo")
                   let id = photo.Attribute("id").Value
                   let title = photo.Attribute("title").Value
                   let secret = photo.Attribute("secret").Value
                   let server = photo.Attribute("server").Value
                   let farm = photo.Attribute("farm").Value
                   select new FlickrResult
                   {
                       Title = title,
                       URL = string.Format("https://farm{0}.staticflickr.com/{1}/{2}_{3}.jpg",
                                            farm, server, id, secret)
                   };
                ImagesList.Clear();             // clear imagesListBox
                                                // set ListBox properties only if results were found
                if (flickrPhotos.Any())
                {
                    // Invoke to ToList on the flickrPhotos LINQ query to convert it to list, then assign the result to the ListBox's DataSource property
                    // Set the ListBox's DisplayMember property to the Title property.
                    ImagesList = new ObservableCollection<FlickrResult>(flickrPhotos);
                    SelectedImage = ImagesList[0];
                } // end if 
                else // no matches were found
                    ImagesList.Add(new FlickrResult { Title = "No matches" });
            } // end try
            catch (WebException)
            {
                // check whether Task failed
                if (_flickrTask.Status == TaskStatus.Faulted)
                    MessageBox.Show("Unable to get results from Flickr",
                       "Flickr Error", MessageBoxButtons.OK,
                       MessageBoxIcon.Error);

                ImagesList.Clear();  // clear imagesListBox
                ImagesList.Add(new FlickrResult { Title = "Error occurred" });
            } // end catch
        } // end method searchButton_Click

        // display selected image
        private async void ImagesListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (SelectedImage != null && SelectedImage.URL != null)
            {
                string selectedURL = SelectedImage.URL;

                // use WebClient to get selected image's bytes asynchronously
                // Create a WebClient object
                WebClient imageClient = new WebClient();

                //Invoke the the WebClient DownloadDataTaskAsync method to get byte array called imageBytes that contains the photo (from selectedURL) and await the results. 
                byte[] imagesBytes = await imageClient.DownloadDataTaskAsync(selectedURL);

                //Create a MemoryStream object from imagesBytes
                MemoryStream memoryStream = new MemoryStream(imagesBytes);

                //Use the Image class's static FromStream method to create an image from the MemoryStream object and assign the image to the PictureBox's image property
                //to display the selected photo
                var imageSource = new BitmapImage();
                imageSource.BeginInit();
                imageSource.StreamSource = memoryStream;
                imageSource.EndInit();

                // Assign the Source property of your image
                DisplayedImage.Source = imageSource;
            } // end if
        } // end method ImagesListBox_SelectedIndexChanged
    } // end class MainViewModel
} // end namespace FlickrViewer