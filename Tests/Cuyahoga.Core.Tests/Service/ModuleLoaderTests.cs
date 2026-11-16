using System;
using System.Collections;
using System.Collections.Generic;
using Castle.MicroKernel;
using Cuyahoga.Core.Domain;
using Cuyahoga.Core.Service;
using Cuyahoga.Core.Service.SiteStructure;
using Xunit;

namespace Cuyahoga.Core.Tests.Service
{
	public class ModuleLoaderTests
	{
		public class TestModule : ModuleBase
		{
			public int ReadSettingsCalls;

			public override void ReadSectionSettings()
			{
				base.ReadSectionSettings();
				ReadSettingsCalls++;
			}
		}

		public class OtherTestModule : ModuleBase
		{
		}

		public interface ITestModuleService
		{
		}

		public class TestModuleService : ITestModuleService
		{
		}

		private class FakeModuleTypeService : IModuleTypeService
		{
			private readonly IList _moduleTypes;

			public FakeModuleTypeService(params ModuleType[] moduleTypes) => _moduleTypes = moduleTypes;

			public IList GetAllModuleTypes() => _moduleTypes;

			public IList GetAllModuleTypesInUse() => throw new NotImplementedException();
			public ModuleType GetModuleById(int moduleTypeId) => throw new NotImplementedException();
			public void SaveModuleType(ModuleType moduleType) => throw new NotImplementedException();
			public void SaveOrUpdateModuleType(ModuleType moduleType) => throw new NotImplementedException();
			public void DeleteModuleType(ModuleType moduleType) => throw new NotImplementedException();
		}

		private class FakeSectionUrlProvider : ISectionUrlProvider
		{
			public string Url;

			public string GetUrlFromSection(Section section) => Url;
		}

		private static ModuleType ModuleTypeFor(Type moduleClass, bool autoActivate = true) =>
			new ModuleType
			{
				ClassName = moduleClass.FullName,
				AssemblyName = moduleClass.Assembly.GetName().Name,
				AutoActivate = autoActivate
			};

		// The test modules don't have NHibernate mappings, so the loader never touches the session factory.
		private static ModuleLoader Loader(IKernel kernel, ISectionUrlProvider urlProvider = null, params ModuleType[] moduleTypes) =>
			new ModuleLoader(kernel, null, new FakeModuleTypeService(moduleTypes), urlProvider ?? new FakeSectionUrlProvider());

		[Fact]
		public void Registers_only_the_module_types_that_activate_automatically()
		{
			var active = ModuleTypeFor(typeof(TestModule));
			var inactive = ModuleTypeFor(typeof(OtherTestModule), autoActivate: false);
			var loader = Loader(new DefaultKernel(), null, active, inactive);

			loader.RegisterActivatedModules();

			Assert.True(loader.IsModuleActive(active));
			Assert.False(loader.IsModuleActive(inactive));
			Assert.IsType<TestModule>(loader.GetModuleFromType(active));
			Assert.Null(loader.GetModuleFromType(inactive));
		}

		[Fact]
		public void Activating_a_module_twice_is_harmless()
		{
			var moduleType = ModuleTypeFor(typeof(TestModule));
			var loader = Loader(new DefaultKernel());

			loader.ActivateModule(moduleType);
			loader.ActivateModule(moduleType);

			Assert.True(loader.IsModuleActive(moduleType));
		}

		[Fact]
		public void Activating_an_unknown_module_type_fails()
		{
			var moduleType = new ModuleType { ClassName = "Nope.Module", AssemblyName = "Nope" };
			var loader = Loader(new DefaultKernel());

			var ex = Assert.Throws<Exception>(() => loader.ActivateModule(moduleType));
			Assert.Contains("Nope.Module, Nope", ex.Message);
		}

		[Fact]
		public void Registers_the_module_services_with_their_lifestyle()
		{
			var moduleType = ModuleTypeFor(typeof(TestModule));
			moduleType.ModuleServices.Add(new ModuleService
			{
				ServiceKey = "test.service",
				ServiceType = typeof(ITestModuleService).AssemblyQualifiedName,
				ClassType = typeof(TestModuleService).AssemblyQualifiedName,
				Lifestyle = "Transient"
			});
			var kernel = new DefaultKernel();

			Loader(kernel).ActivateModule(moduleType);

			Assert.NotSame(kernel.Resolve<ITestModuleService>(), kernel.Resolve<ITestModuleService>());
		}

		[Fact]
		public void A_module_service_with_an_invalid_lifestyle_fails()
		{
			var moduleType = ModuleTypeFor(typeof(TestModule));
			moduleType.ModuleServices.Add(new ModuleService
			{
				ServiceKey = "test.service",
				ServiceType = typeof(ITestModuleService).AssemblyQualifiedName,
				ClassType = typeof(TestModuleService).AssemblyQualifiedName,
				Lifestyle = "Forever"
			});

			var ex = Assert.Throws<Exception>(() => Loader(new DefaultKernel()).ActivateModule(moduleType));
			Assert.Contains("Forever", ex.Message);
		}

		[Fact]
		public void The_module_of_a_section_gets_the_section_its_url_and_its_settings()
		{
			var moduleType = ModuleTypeFor(typeof(TestModule));
			var section = new Section { Id = 7, ModuleType = moduleType };
			var loader = Loader(new DefaultKernel(), new FakeSectionUrlProvider { Url = "/7/section.aspx" });
			loader.ActivateModule(moduleType);

			var module = (TestModule)loader.GetModuleFromSection(section);

			Assert.Same(section, module.Section);
			Assert.Equal("/7/section.aspx", module.SectionUrl);
			Assert.Equal(1, module.ReadSettingsCalls);
		}

		[Fact]
		public void Without_a_url_the_section_url_of_the_module_is_left_alone()
		{
			var moduleType = ModuleTypeFor(typeof(TestModule));
			var loader = Loader(new DefaultKernel(), new FakeSectionUrlProvider { Url = null });
			loader.ActivateModule(moduleType);

			var module = loader.GetModuleFromSection(new Section { Id = 7, ModuleType = moduleType });

			Assert.Null(module.SectionUrl);
		}

		[Fact]
		public void A_section_of_an_inactive_module_has_no_module()
		{
			var moduleType = ModuleTypeFor(typeof(TestModule));
			var loader = Loader(new DefaultKernel());

			Assert.Null(loader.GetModuleFromSection(new Section { Id = 7, ModuleType = moduleType }));
		}
	}
}
