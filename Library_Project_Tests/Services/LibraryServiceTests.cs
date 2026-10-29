using Library_Project.Model;
using Library_Project.Services;
using Library_Project.Services.Interfaces;
using Moq;

namespace Library_Project_Tests.Services
{
    /// <summary>
    /// Клас для модульного тестування бізнес-логіки LibraryService.
    /// Використовує xUnit для тестів та Moq для створення імітацій (моків) залежностей.
    /// </summary>
    public class LibraryServiceTests
    {
        private readonly Mock<IBookRepository> _repoMock;
        private readonly Mock<IMemberService> _memberMock;
        private readonly Mock<INotificationService> _notifMock;
        
        private readonly LibraryService _service;

        /// <summary>
        /// Конструктор ініціалізує моки та створює екземпляр LibraryService 
        /// перед виконанням кожного тесту.
        /// </summary>
        public LibraryServiceTests()
        {
            _repoMock = new Mock<IBookRepository>();
            _memberMock = new Mock<IMemberService>();
            _notifMock = new Mock<INotificationService>();

            _service = new LibraryService(_repoMock.Object, _memberMock.Object, _notifMock.Object);
        }
        
        /// <summary>
        /// Перевіряє, що метод генерує виняток ArgumentException, якщо назва книги є некоректною (null, порожня або складається з пробілів).
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void AddBook_ThrowsArgumentException_WhenTitleIsInvalid(string invalidTitle)
        {
            // Arrange
            const int copies = 5;

            // Act & Assert
            Assert.Throws<ArgumentException>(() => _service.AddBook(invalidTitle, copies));
        }

        /// <summary>
        /// Перевіряє, що метод генерує виняток ArgumentException, якщо кількість копій є нульовою або від'ємною.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public void AddBook_ThrowsArgumentException_WhenCopiesAreInvalid(int invalidCopies)
        {
            // Arrange
            const string validTitle = "Valid Title";

            // Act & Assert
            Assert.Throws<ArgumentException>(() => _service.AddBook(validTitle, invalidCopies));
        }

        /// <summary>
        /// Перевіряє, що метод створює та зберігає нову книгу, якщо книги з такою назвою ще немає в базі.
        /// </summary>
        [Fact]
        public void AddBook_SavesNewBook_WhenBookDoesNotExist()
        {
            // Arrange
            const string title = "New Book";
            const int copies = 5;
            _repoMock.Setup(r => r.FindBook(title)).Returns((Book)null);

            // Act
            _service.AddBook(title, copies);

            // Assert
            _repoMock.Verify(r => r.SaveBook(It.Is<Book>(b => b.Title == title && b.Copies == copies)), Times.Exactly(1));
        }

        /// <summary>
        /// Перевіряє, що метод коректно оновлює кількість примірників існуючої книги та зберігає зміни.
        /// </summary>
        [Fact]
        public void AddBook_UpdatesExistingBook_WhenBookAlreadyExists()
        {
            // Arrange
            const string title = "Existing Book";
            const int initialCopies = 3;
            const int addedCopies = 2;
            const int expectedCopies = 5;
            var existingBook = new Book { Title = title, Copies = initialCopies };
            _repoMock.Setup(r => r.FindBook(title)).Returns(existingBook);

            // Act
            _service.AddBook(title, addedCopies);

            // Assert
            Assert.Equal(expectedCopies, existingBook.Copies);
            _repoMock.Verify(r => r.SaveBook(existingBook), Times.Exactly(1));
        }
        
        /// <summary>
        /// Перевіряє, що метод генерує виняток InvalidOperationException, якщо ідентифікатор читача недійсний.
        /// Також перевіряє, що жодні зміни не зберігаються, а нотифікації не надсилаються.
        /// </summary>
        [Fact]
        public void BorrowBook_ThrowsInvalidOperationException_WhenMemberIsInvalid()
        {
            // Arrange
            const int invalidMemberId = 999;
            const string title = "Some Book";
            
            _memberMock.Setup(m => m.IsValidMember(invalidMemberId)).Returns(false);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _service.BorrowBook(invalidMemberId, title));
            
            _repoMock.Verify(r => r.SaveBook(It.IsAny<Book>()), Times.Never);
            _notifMock.Verify(n => n.NotifyBorrow(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        /// <summary>
        /// Перевіряє, що метод повертає false, якщо книгу знайдено, але кількість її копій дорівнює нулю або менше.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(-1)] 
        public void BorrowBook_ReturnsFalse_WhenBookHasNoCopies(int copies)
        {
            // Arrange
            const int validMemberId = 1;
            const string title = "Unavailable Book";
            
            _memberMock.Setup(m => m.IsValidMember(validMemberId)).Returns(true);
            
            var existingBook = new Book { Title = title, Copies = copies };
            _repoMock.Setup(r => r.FindBook(title)).Returns(existingBook);

            // Act
            bool result = _service.BorrowBook(validMemberId, title);

            // Assert
            Assert.False(result);
            _repoMock.Verify(r => r.SaveBook(It.IsAny<Book>()), Times.Never);
            _notifMock.Verify(n => n.NotifyBorrow(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        /// <summary>
        /// Перевіряє, що метод повертає false, якщо книга взагалі відсутня (репозиторій повертає null).
        /// </summary>
        [Fact]
        public void BorrowBook_ReturnsFalse_WhenBookIsNull()
        {
            // Arrange
            const int validMemberId = 1;
            const string title = "NonExistent Book";
            
            _memberMock.Setup(m => m.IsValidMember(validMemberId)).Returns(true);
            _repoMock.Setup(r => r.FindBook(title)).Returns((Book)null);

            // Act
            bool result = _service.BorrowBook(validMemberId, title);

            // Assert
            Assert.False(result);
            _repoMock.Verify(r => r.SaveBook(It.IsAny<Book>()), Times.Never);
        }

        /// <summary>
        /// Перевіряє успішний сценарій взяття книги: зменшення кількості примірників, збереження в базу та відправку нотифікації.
        /// </summary>
        [Fact]
        public void BorrowBook_ReturnsTrueAndDecreasesCopies_WhenSuccessful()
        {
            // Arrange
            const int validMemberId = 1;
            const string title = "Available Book";
            const int initialCopies = 2;
            const int expectedCopies = 1;
            
            _memberMock.Setup(m => m.IsValidMember(validMemberId)).Returns(true);
            
            var availableBook = new Book { Title = title, Copies = initialCopies };
            _repoMock.Setup(r => r.FindBook(title)).Returns(availableBook);

            // Act
            bool result = _service.BorrowBook(validMemberId, title);

            // Assert
            Assert.True(result);
            Assert.NotEqual(initialCopies, availableBook.Copies); 
            Assert.Equal(expectedCopies, availableBook.Copies);
            
            _repoMock.Verify(r => r.SaveBook(availableBook), Times.Exactly(1));
            _notifMock.Verify(n => n.NotifyBorrow(validMemberId, title), Times.Exactly(1));
        }
        
        /// <summary>
        /// Перевіряє, що метод повертає false, якщо книга, яку намагаються повернути, не знайдена в базі.
        /// Також переконаємося, що не викликаються методи збереження та сповіщення.
        /// </summary>
        [Fact]
        public void ReturnBook_ReturnsFalse_WhenBookIsNotFound()
        {
            // Arrange
            const int memberId = 1;
            const string title = "Unknown Book";
            
            _repoMock.Setup(r => r.FindBook(title)).Returns((Book)null);

            // Act
            bool result = _service.ReturnBook(memberId, title);

            // Assert
            Assert.False(result);
            _repoMock.Verify(r => r.SaveBook(It.IsAny<Book>()), Times.Never);
            _notifMock.Verify(n => n.NotifyReturn(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        /// <summary>
        /// Перевіряє успішний сценарій повернення книги: збільшення кількості примірників, збереження змін та відправку нотифікації.
        /// </summary>
        [Fact]
        public void ReturnBook_ReturnsTrueAndIncreasesCopies_WhenSuccessful()
        {
            // Arrange
            const int memberId = 1;
            const string title = "Returned Book";
            const int initialCopies = 1;
            const int expectedCopies = 2;
            
            var existingBook = new Book { Title = title, Copies = initialCopies };
            _repoMock.Setup(r => r.FindBook(title)).Returns(existingBook);

            // Act
            bool result = _service.ReturnBook(memberId, title);

            // Assert
            Assert.True(result);
            Assert.Equal(expectedCopies, existingBook.Copies);
            
            _repoMock.Verify(r => r.SaveBook(existingBook), Times.Exactly(1));
            _notifMock.Verify(n => n.NotifyReturn(memberId, title), Times.Exactly(1));
        }
        
        /// <summary>
        /// Перевіряє, що метод повертає список, який містить лише ті книги, кількість примірників яких більша за нуль,
        /// і не повертає null або порожню колекцію.
        /// </summary>
        [Fact]
        public void GetAvailableBooks_ReturnsOnlyBooksWithCopiesGreaterThanZero()
        {
            // Arrange
            const string availableTitle = "Available Book";
            const string unavailableTitle = "Unavailable Book";
            
            var availableBook = new Book { Title = availableTitle, Copies = 2 };
            var unavailableBook = new Book { Title = unavailableTitle, Copies = 0 };
            
            var allBooks = new List<Book> { availableBook, unavailableBook };
            _repoMock.Setup(r => r.GetAllBooks()).Returns(allBooks);

            // Act
            var result = _service.GetAvailableBooks();

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            Assert.Contains(availableBook, result);
            Assert.DoesNotContain(unavailableBook, result);
        }

        /// <summary>
        /// Перевіряє, що метод повертає порожній список, якщо всі книги в базі мають нульову кількість примірників.
        /// </summary>
        [Fact]
        public void GetAvailableBooks_ReturnsEmptyList_WhenNoBooksAreAvailable()
        {
            // Arrange
            const string title1 = "Book One";
            const string title2 = "Book Two";
            
            var allBooks = new List<Book> 
            { 
                new Book { Title = title1, Copies = 0 },
                new Book { Title = title2, Copies = -1 }
            };
            
            _repoMock.Setup(r => r.GetAllBooks()).Returns(allBooks);

            // Act
            var result = _service.GetAvailableBooks();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }
    }
}