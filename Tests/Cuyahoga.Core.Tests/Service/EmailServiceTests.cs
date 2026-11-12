using System;
using System.Collections.Generic;
using System.IO;
using Cuyahoga.Core;
using Cuyahoga.Core.Service.Email;
using Xunit;

namespace Cuyahoga.Core.Tests.Service
{
	/// <summary>
	/// DefaultEmailService with the real template engine on template files in a temporary directory, and a sender that
	/// records the emails.
	/// </summary>
	public sealed class EmailServiceTests : IDisposable
	{
		private class RecordingEmailSender : IEmailSender
		{
			public readonly List<(string From, string To, string Subject, string Body)> Sent = new List<(string, string, string, string)>();
			public bool Fail { get; set; }

			public void Send(string from, string to, string subject, string body)
			{
				if (Fail)
				{
					throw new InvalidOperationException("SMTP server unavailable");
				}
				Sent.Add((from, to, subject, body));
			}

			public void Send(string from, string to, string subject, string body, string[] cc, string[] bcc) =>
				Send(from, to, subject, body);
		}

		private readonly TempDirectory _templates = new TempDirectory();
		private readonly RecordingEmailSender _sender = new RecordingEmailSender();

		public void Dispose() => _templates.Dispose();

		private void Template(string fileName, string subject, string body) =>
			// The template engine expects Windows line endings.
			File.WriteAllText(_templates.File(fileName), $"[subject]\r\n{subject}\r\n[/subject]\r\n[body]\r\n{body}\r\n[/body]\r\n");

		private DefaultEmailService Service(string language = null) =>
			new DefaultEmailService(_sender, new SimpleEmailTemplateEngine()) { TemplateDir = _templates.Path, Language = language };

		private void SendRegister(DefaultEmailService service) =>
			service.ProcessEmail("site@example.com", "ann@example.com", "Register",
				new Dictionary<string, string> { { "{site}", "Cuyahoga" } },
				new Dictionary<string, string> { { "{user}", "Ann" }, { "{password}", "pw123" } });

		[Fact]
		public void Fills_in_the_template_and_sends_the_email()
		{
			Template("Register.txt", "Welcome to {site}", "Hello {user},\r\nyour password is {password}.");

			SendRegister(Service());

			var email = Assert.Single(_sender.Sent);
			Assert.Equal(("site@example.com", "ann@example.com"), (email.From, email.To));
			Assert.Equal("Welcome to Cuyahoga", email.Subject);
			Assert.Equal("Hello Ann,\r\nyour password is pw123.", email.Body);
		}

		[Fact]
		public void Uses_the_template_of_the_language_when_there_is_one()
		{
			Template("Register.txt", "Welcome", "Hello");
			Template("Register.nl.txt", "Welkom", "Hallo");

			SendRegister(Service("NL"));
			SendRegister(Service("de"));

			Assert.Equal("Welkom", _sender.Sent[0].Subject);
			Assert.Equal("Welcome", _sender.Sent[1].Subject);
		}

		[Fact]
		public void Missing_template()
		{
			Assert.Throws<FileNotFoundException>(() => SendRegister(Service()));
			Assert.Empty(_sender.Sent);
		}

		[Fact]
		public void Send_failure_becomes_an_EmailException()
		{
			Template("Register.txt", "Welcome", "Hello");
			_sender.Fail = true;

			var exception = Assert.Throws<EmailException>(() => SendRegister(Service()));
			Assert.IsType<InvalidOperationException>(exception.InnerException);
		}
	}
}
