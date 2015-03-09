﻿
module vdb.tests.viewModels.tags {
	
	import vm = vdb.viewModels;

	var viewModel: vm.tags.TagsEditViewModel;

	QUnit.module("ArtistRolesEditViewModel", {
		setup: () => {

			viewModel = new vm.tags.TagsEditViewModel(null);
			viewModel.invalidTagError = () => { };

		}
	});

	QUnit.test("addTag - new tag", () => {

		viewModel.newTagName("Miku");

		viewModel.addTag();

		QUnit.equal(viewModel.selections().length, 1, "selections.length");

		var selection = viewModel.selections()[0];
		QUnit.equal(selection.tagName, "Miku", "selection.tagName");
		QUnit.equal(selection.selected(), true, "selection.selected");

		QUnit.equal(viewModel.newTagName(), "", "newTagName");

	});

	QUnit.test("addTag - already exists", () => {

		var selection = new vm.tags.TagSelectionViewModel({ tagName: 'Miku' });
		viewModel.selections.push(selection);
		QUnit.equal(selection.selected(), false, "selection.selected");
		viewModel.newTagName("Miku");

		viewModel.addTag();

		QUnit.equal(selection.selected(), true, "selection.selected");

	});

	QUnit.test("addTag - convert spaces",() => {

		viewModel.newTagName("Kyary Pamyu Pamyu");

		viewModel.addTag();

		var selection = viewModel.selections()[0];
		QUnit.equal(selection.tagName, "Kyary_Pamyu_Pamyu", "selection.tagName");

	});

	QUnit.test("addTag - invalid tag",() => {

		viewModel.newTagName("Miku!");

		viewModel.addTag();

		QUnit.equal(viewModel.selections().length, 0, "selections.length");

	});

} 