using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Platform.VirtualFileSystem.Tests
{
	[TestFixture]
	public class BasicTests
		: TestsBase
	{
		[Test]
		public void Sample()
		{
			var dir = FileSystemManager.Default.ResolveDirectory("C:/Windows");
			var fileSystem = dir.CreateView("windows");
			var system32 = fileSystem.ResolveDirectory("System32");

			// Will output: windows://System32
			Console.WriteLine(system32.Address);

			((StandardFileSystemManager)FileSystemManager.Default).AddFileSystem(fileSystem);

			var windowsNotepad =  FileSystemManager.Default.ResolveFile("windows://notepad.exe");

			Console.WriteLine(windowsNotepad.Exists);

			var root = dir.ResolveFile("..");

			// Will output: file://C:/
			Console.WriteLine(root.Address);

			var notepad = dir.ResolveFile("notepad.exe");

			// Will output: True
			Console.WriteLine(notepad.Exists);

			// Will output: /Windows/notepad.exe
			Console.WriteLine(root.Address.GetRelativePathTo(notepad.Address));


			Console.WriteLine(notepad.Address.GetRelativePathTo(root.Address));
		}

		[Test]
		public void Test_Temp_FileSystem()
		{
			var file = FileSystemManager.Default.ResolveFile("temp:///Temp.txt");

			Assert.IsTrue(file.Exists);
		}

		[Test]
		public void Test_Check_TestFiles_View_FileSystem_Exists()
		{
			var dir = FileSystemManager.Default.ResolveDirectory("testfiles://Directory1");

			Assert.IsTrue(dir.Exists);
		}

		[Test]
		public void Test_View_Defined_Inside_App_Config()
		{
			var file = FileSystemManager.Default.ResolveFile("testview:///Temp.txt");

			Assert.IsTrue(file.Exists);
		}

		[Test]
		public void Test_NodeOperationFilter()
		{
			var file = this.WorkingDirectory.ResolveFile("NewFile.txt");

			var createCount = OperationFilter.numberOfTimesCreateCalled;
			var deleteCount = OperationFilter.numberOfTimesDeleteCalled;

			file.Create();

			Assert.AreEqual(OperationFilter.numberOfTimesCreateCalled, createCount + 1);
			Assert.AreEqual(OperationFilter.numberOfTimesDeleteCalled, deleteCount);

			file.Delete();

			Assert.AreEqual(OperationFilter.numberOfTimesCreateCalled, createCount + 1);
			Assert.AreEqual(OperationFilter.numberOfTimesDeleteCalled, deleteCount + 1);
		}

		[Test]
		public void Test_Create_View()
		{
			var fileSystem =  this.WorkingDirectory.CreateView("myview");

			var rootDirectory =  fileSystem.ResolveDirectory("/");

			Assert.AreEqual(this.WorkingDirectory.GetNativePath(), rootDirectory.GetNativePath());
			Assert.AreNotEqual(this.WorkingDirectory.FileSystem.RootDirectory, rootDirectory);
		}

		[Test]
		[ExpectedException(typeof(ArgumentException))]
		public void Test_Create_View_And_Go_Above_Root()
		{
			var fileSystem = this.WorkingDirectory.CreateView("myview");

			fileSystem.RootDirectory.ResolveDirectory("..");
		}

		[Test]
		public void Test_GetContent()
		{
			var file1 = this.WorkingDirectory.ResolveFile("TextFile1.txt");

			using (var reader = file1.GetContent().GetReader())
			{
				Assert.AreEqual(reader.ReadToEnd(), "TextFile1.txt");
			}
		}

		[Test]
		public void Test_GetChildren()
		{
			var children = this.WorkingDirectory.GetChildren(NodeType.Any).ToList();

			Assert.AreEqual(6, children.Count);

			var childNamesSetchildrenSet = new HashSet<string>(this.WorkingDirectory.GetChildren(NodeType.Any).Select(c => c.Name));

			Assert.IsTrue(childNamesSetchildrenSet.Contains("Directory1"));
			Assert.IsTrue(childNamesSetchildrenSet.Contains("Temp"));
			Assert.IsTrue(childNamesSetchildrenSet.Contains("TextFile1.txt"));
			Assert.IsTrue(childNamesSetchildrenSet.Contains("TextFile2.txt"));
			Assert.IsTrue(childNamesSetchildrenSet.Contains("DataFile1.xml"));

			childNamesSetchildrenSet = new HashSet<string>(this.WorkingDirectory.GetChildNames(NodeType.Any));

			Assert.IsTrue(childNamesSetchildrenSet.Contains("Directory1"));
			Assert.IsTrue(childNamesSetchildrenSet.Contains("TextFile1.txt"));
			Assert.IsTrue(childNamesSetchildrenSet.Contains("TextFile2.txt"));

			children = this.WorkingDirectory.GetChildren(NodeType.Directory).ToList();

			Assert.AreEqual(children.Count, 3);

			children = this.WorkingDirectory.GetChildren(NodeType.File).ToList();

			Assert.AreEqual(3, children.Count);
		}

		[Test]
		public void Test_NodeAddress()
		{
			var file1 = this.WorkingDirectory.ResolveFile("Directory1/SubDirectory1/A.csv");
			var file2 = this.WorkingDirectory.ResolveFile("Directory1/../Directory1/../Directory1/SubDirectory1/../SubDirectory1/A.csv");
			
			Assert.AreEqual(file1, file2);
			Assert.AreSame(file1, file2);

			var nodeAddressOfParentMethod1 = file1.Address.Parent;
			var nodeAddressOfParentMethod2 = file1.Address.ResolveAddress("..");

			Assert.AreEqual(nodeAddressOfParentMethod1, nodeAddressOfParentMethod2);
			Assert.AreEqual(nodeAddressOfParentMethod1.AbsolutePath, nodeAddressOfParentMethod2.AbsolutePath);

			var relativePath = this.WorkingDirectory.Address.GetRelativePathTo(file1.Address);

			Assert.AreEqual(relativePath, "Directory1/SubDirectory1/A.csv");

			relativePath = file1.Address.GetRelativePathTo(this.WorkingDirectory.Address);

			Assert.AreEqual(relativePath, "../../..");

			var dir = file1.ResolveDirectory("../..");

			Assert.AreEqual(this.WorkingDirectory, dir);
		}

		[Test]
		public void Test_Resolve_File_And_Check_Exists()
		{
			Assert.IsTrue(this.WorkingDirectory.Exists);

			var testfile1 = this.WorkingDirectory.ResolveFile("TextFile1.txt");

			Assert.IsTrue(testfile1.Exists);

			var testfile2 = this.WorkingDirectory.ResolveFile("TextFile2.txt");

			Assert.IsTrue(testfile2.Exists);

			var txtFileA = this.WorkingDirectory.ResolveFile("Directory1/A.txt");

			Assert.IsTrue(txtFileA.Exists);

			var txtFileB = txtFileA.ParentDirectory.ResolveFile("B.txt");

			Assert.IsTrue(txtFileB.Exists);

			Assert.AreEqual(txtFileA.ParentDirectory, txtFileB.ParentDirectory);
			Assert.AreSame(txtFileA.ParentDirectory, txtFileB.ParentDirectory);

			var nonExistantFile = this.WorkingDirectory.ResolveFile("NonExistantFile.txt");

			Assert.IsFalse(nonExistantFile.Exists);
		}
	}
}

