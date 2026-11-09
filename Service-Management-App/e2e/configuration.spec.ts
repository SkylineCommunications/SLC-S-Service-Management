import { expect, test } from '@playwright/test';
import { mapServiceConfigurationRows } from '../src/dataminer/services.js';
import { APP_URL, mockDataMinerApi, setSessionCookie } from './fixtures';

test('preserves repeated parameter definitions and deduplicates only repeated instances', () => {
    const base = { 'Version ID': 'version', 'Parameter ID': 'port', 'Parameter Name': 'Port', 'Parameter Type': 'Number' };
    const main = { ...base, 'Configuration ID': 'main', 'Parameter Value ID': 'main-value', 'Parameter Label': 'Main', 'Parameter Value': '5000' };
    const backup = { ...base, 'Configuration ID': 'backup', 'Parameter Value ID': 'backup-value', 'Parameter Label': 'Backup', 'Parameter Value': '5001' };
    const profileMain = { ...base, 'Profile ID': 'profile', 'Parameter Value ID': 'profile-main', 'Parameter Label': 'Main' };
    const profileBackup = { ...profileMain, 'Parameter Value ID': 'profile-backup', 'Parameter Label': 'Backup' };
    const result = mapServiceConfigurationRows([main, main, backup, profileMain, profileMain, profileBackup]);
    expect(result.versions[0].parameters.map(parameter => parameter.label)).toEqual(['Backup', 'Main']);
    expect(result.versions[0].profiles[0].parameters.map(parameter => parameter.label)).toEqual(['Backup', 'Main']);
});

test('sorts configuration parameters, profiles and profile parameters alphabetically', () => {
    const version = { 'Version ID': 'version' };
    const parameter = (id: string, name: string, extra = {}) => ({ ...version, 'Parameter ID': id, 'Parameter Name': name, 'Configuration ID': `${id}-config`, 'Parameter Value ID': `${id}-value`, ...extra });
    const profile = (id: string, name: string) => ({ ...version, 'Profile ID': id, 'Profile Name': name });
    const result = mapServiceConfigurationRows([
        parameter('zeta', 'zeta'), parameter('alpha', 'Alpha'), parameter('port10', 'Port 10'), parameter('port2', 'Port 2'),
        { ...profile('video', 'Video'), 'Parameter ID': 'res', 'Parameter Name': 'Resolution', 'Parameter Value ID': 'res-value' },
        { ...profile('video', 'Video'), 'Parameter ID': 'bit', 'Parameter Name': 'Bitrate', 'Parameter Value ID': 'bit-value' },
        { ...profile('video', 'Video'), 'Nested Profile ID': 'second', 'Nested Profile Name': 'Second' },
        { ...profile('video', 'Video'), 'Nested Profile ID': 'first', 'Nested Profile Name': 'First' },
        { ...profile('audio', 'audio'), 'Parameter ID': 'gain', 'Parameter Name': 'Gain', 'Parameter Value ID': 'gain-value' },
        { 'Record Type': 'AvailableParameter', 'Parameter ID': 'z', 'Parameter Name': 'Zoom' },
        { 'Record Type': 'AvailableParameter', 'Parameter ID': 'a', 'Parameter Name': 'Aspect' },
    ]);
    const [mapped] = result.versions;
    expect(mapped.parameters.map(item => item.name)).toEqual(['Alpha', 'Port 2', 'Port 10', 'zeta']);
    expect(mapped.profiles.map(item => item.name)).toEqual(['audio', 'Video']);
    expect(mapped.profiles[1].parameters.map(item => item.name)).toEqual(['Bitrate', 'Resolution']);
    expect(mapped.profiles[1].nestedProfiles.map(item => item.name)).toEqual(['First', 'Second']);
    expect(result.availableParameters.map(item => item.name)).toEqual(['Aspect', 'Zoom']);
});

test('reuses profile parameters with distinct labels and independent values', async ({ page, context }) => {
    await setSessionCookie(context);
    const mock = await mockDataMinerApi(page);
    await page.goto(APP_URL);
    await page.getByRole('navigation', { name: 'Primary navigation' }).getByRole('link', { name: 'inventory_2 Catalog' }).click();
    await page.getByRole('button', { name: 'Open details of Managed switching', exact: true }).click();
    await page.getByRole('button', { name: 'Expand profiles', exact: true }).click();
    await page.getByRole('button', { name: 'Expand Transport profile', exact: true }).click();
    const profile = page.locator('.configuration-profile-card').first();
    for (let index = 0; index < 2; index++) {
        await profile.getByLabel('Select parameter for Transport profile', { exact: true }).selectOption('parameter-number');
        await profile.locator('.configuration-profile-parameters .configuration-add-controls').getByRole('button', { name: 'Add', exact: true }).click();
    }
    const labels = profile.getByLabel('Label for Max Overall Bitrate', { exact: true });
    await expect(labels).toHaveCount(2);
    await expect(labels.nth(0)).toHaveValue('Max Overall Bitrate');
    await expect(labels.nth(1)).toHaveValue('Max Overall Bitrate #2');
    await labels.nth(0).fill('Main');
    await labels.nth(1).fill('Backup');
    const values = profile.getByLabel('Value for Max Overall Bitrate', { exact: true });
    await values.nth(0).fill('30');
    await values.nth(1).fill('40');
    await page.getByRole('button', { name: 'Show Value Details', exact: true }).click();
    await expect(values.nth(0)).toHaveValue('30');
    await expect(values.nth(1)).toHaveValue('40');
    await values.nth(1).fill('50');
    await expect(values.nth(0)).toHaveValue('30');
    await labels.nth(1).fill(' MAIN ');
    await page.getByRole('button', { name: 'Save configuration', exact: true }).click();
    await expect(page.getByRole('alert')).toContainText('Transport profile: Repeated parameter');
    const saves = () => mock.calls
        .filter(call => call.method === 'ExecuteAutomationScriptWithOutput' && call.body.script?.Name === 'SLC_SM_UDAPI_ServiceManagement')
        .map(call => JSON.parse(call.body.script.Parameters.find((parameter: { Name: string }) => parameter.Name === 'Request').Value))
        .filter(request => request.RequestMethod === 2);
    expect(saves()).toHaveLength(0);
    await labels.nth(1).fill('Backup');
    await profile.getByRole('button', { name: 'Delete Max Overall Bitrate', exact: true }).first().click();
    await expect(labels).toHaveCount(1);
    await expect(labels).toHaveValue('Backup');
    await expect(values).toHaveValue('50');
    await page.getByRole('button', { name: 'Save configuration', exact: true }).click();
    await expect.poll(() => saves().length).toBe(1);
    expect(JSON.parse(saves()[0].Body).Profiles[0].Parameters).toEqual([
        expect.objectContaining({ ParameterId: 'parameter-number', Label: 'Backup', Value: '50', ParameterValueId: '' }),
    ]);
});

test('prefills a new service configuration version from the specification template', async ({ page, context }) => {
    await setSessionCookie(context);
    await mockDataMinerApi(page);
    const template = { versionId: 'SpecificationTemplate', versionName: 'Managed switching', isActive: 'false' };
    const profile = { ...template, profileDefinitionId: 'transport', profileDefinitionName: 'Transport' };
    const records = [
        { recordType: 'AvailableParameter', parameterId: 'port', parameterName: 'Port', parameterType: 'Number' },
        { recordType: 'AvailableProfile', profileDefinitionId: 'transport', profileDefinitionName: 'Transport' },
        { recordType: 'AvailableNestedProfile', nestedProfileId: 'shared-profile', nestedProfileName: 'Shared', nestedProfileDefinitionId: 'transport', profileReusable: true },
        {
            ...template, recordType: 'Parameter', configurationId: 'spec-configuration', parameterValueId: 'spec-value',
            parameterId: 'port', parameterName: 'Port', parameterType: 'Number', parameterValue: '5000', mandatory: true,
        },
        { ...profile, recordType: 'ProfileParameter', profileId: 'own-profile', profileName: 'Own', parameterId: 'port', parameterValueId: 'own-value', parameterName: 'Port', parameterType: 'Number', parameterValue: '1' },
        { ...profile, recordType: 'Profile', profileId: 'shared-profile', profileName: 'Shared', profileReusable: true },
    ];
    let saved: { Parameters: Record<string, unknown>[]; Profiles: Record<string, unknown>[] } | undefined;
    await page.route('**/API/v1/Json.asmx/ExecuteAutomationScriptWithOutput', async route => {
        const body = route.request().postDataJSON();
        if (body.script?.Name !== 'SLC_SM_UDAPI_ServiceManagement') return route.fallback();
        const request = JSON.parse(body.script.Parameters.find((parameter: { Name: string }) => parameter.Name === 'Request').Value);
        if (request.Route !== 'service-management/v1/service/service-1/configuration') return route.fallback();
        if (request.RequestMethod === 2) saved = JSON.parse(request.Body);
        await route.fulfill({ json: { d: { ErrorCode: 0, Output: [
            { Name: 'StatusCode', Value: '200' }, { Name: 'Response', Value: JSON.stringify(records) },
        ] } } });
    });
    await page.goto(APP_URL);
    await page.getByRole('navigation', { name: 'Primary navigation' }).getByRole('link', { name: /Services/ }).click();
    await page.getByRole('button', { name: 'Open details of Test service', exact: true }).click();
    await page.getByRole('button', { name: 'Configurations', exact: true }).click();
    await page.getByRole('button', { name: 'Add initial version', exact: true }).click();
    const section = page.locator('.native-configuration-parameters');
    await section.getByRole('button', { name: 'Expand standalone parameters' }).click();
    await expect(section.getByLabel('Value for Port', { exact: true })).toHaveValue('5000');
    await page.getByRole('button', { name: 'Save configuration', exact: true }).click();
    await expect.poll(() => saved).toBeTruthy();
    expect(saved!.Parameters).toEqual([
        expect.objectContaining({ ConfigurationId: '', ParameterValueId: '', ParameterId: 'port', Value: '5000', Mandatory: true }),
    ]);
    expect(saved!.Profiles).toEqual([
        expect.objectContaining({ id: '', name: 'Own', definitionId: 'transport', Parameters: [expect.objectContaining({ ParameterValueId: '', Value: '1' })] }),
        expect.objectContaining({ id: 'shared-profile', reusable: true }),
    ]);
});

for (const owner of ['service', 'specification']) {
    test(`reuses parameter definitions independently in ${owner} configurations`, async ({ page, context }) => {
        await setSessionCookie(context);
        await mockDataMinerApi(page);
        const resource = owner === 'service' ? 'service/service-1' : 'serviceSpecification/spec-1';
        const version = { versionId: 'spec-1', versionName: 'Current version', isActive: 'true' };
        let parameters = [
            { ConfigurationId: 'main', ParameterValueId: 'main-value', ParameterId: 'port', Label: 'Main', Value: '5000' },
            { ConfigurationId: 'backup', ParameterValueId: 'backup-value', ParameterId: 'port', Label: 'Backup', Value: '5001' },
        ];
        let saved: { Parameters: typeof parameters; DeleteConfigurationIds: string[] } | undefined;
        await page.route('**/API/v1/Json.asmx/ExecuteAutomationScriptWithOutput', async route => {
            const body = route.request().postDataJSON();
            if (body.script?.Name !== 'SLC_SM_UDAPI_ServiceManagement') return route.fallback();
            const request = JSON.parse(body.script.Parameters.find((parameter: { Name: string }) => parameter.Name === 'Request').Value);
            if (request.Route !== `service-management/v1/${resource}/configuration`) return route.fallback();
            if (request.RequestMethod === 2) {
                saved = JSON.parse(request.Body);
                parameters = saved!.Parameters.map((parameter, index) => ({
                    ...parameter,
                    ConfigurationId: parameter.ConfigurationId || `created-${index}`,
                    ParameterValueId: parameter.ParameterValueId || `created-value-${index}`,
                }));
            }
            const records = [
                { ...version, recordType: 'Version' },
                { recordType: 'AvailableParameter', parameterId: 'port', parameterName: 'Port', parameterType: 'Number' },
                ...parameters.map(parameter => ({
                    ...version, recordType: 'Parameter', configurationId: parameter.ConfigurationId,
                    parameterValueId: parameter.ParameterValueId, parameterId: parameter.ParameterId,
                    parameterName: 'Port', parameterType: 'Number', parameterLabel: parameter.Label, parameterValue: parameter.Value,
                })),
            ];
            await route.fulfill({ json: { d: { ErrorCode: 0, Output: [
                { Name: 'StatusCode', Value: '200' }, { Name: 'Response', Value: JSON.stringify(records) },
            ] } } });
        });
        await page.goto(APP_URL);
        const navigation = page.getByRole('navigation', { name: 'Primary navigation' });
        if (owner === 'service') {
            await navigation.getByRole('link', { name: /Services/ }).click();
            await page.getByRole('button', { name: 'Open details of Test service', exact: true }).click();
            await page.getByRole('button', { name: 'Configurations', exact: true }).click();
        } else {
            await navigation.getByRole('link', { name: 'inventory_2 Catalog' }).click();
            await page.getByRole('button', { name: 'Open details of Managed switching', exact: true }).click();
        }
        const section = page.locator('.native-configuration-parameters');
        await section.getByRole('button', { name: 'Expand standalone parameters' }).click();
        const rows = section.locator('tbody tr');
        await expect(rows).toHaveCount(2);
        await expect(rows.first().getByLabel('Label for Port', { exact: true })).toHaveValue('Backup');
        await rows.first().getByLabel('Value for Port', { exact: true }).fill('6001');
        await expect(rows.nth(1).getByLabel('Value for Port', { exact: true })).toHaveValue('5000');
        for (const [label, value] of [['Extra A', '7000'], ['Extra B', '7001']]) {
            await section.getByLabel('Select parameter to add').selectOption('port');
            await section.getByRole('button', { name: 'Add', exact: true }).click();
            await expect(rows.last().getByLabel('Label for Port', { exact: true })).toHaveValue('Port');
            await rows.last().getByLabel('Label for Port', { exact: true }).fill(label);
            await rows.last().getByLabel('Value for Port', { exact: true }).fill(value);
        }
        await expect(rows).toHaveCount(4);
        await expect(rows.nth(1).getByLabel('Label for Port', { exact: true })).toHaveValue('Extra A');
        await rows.nth(1).getByRole('button', { name: 'Delete Port', exact: true }).click();
        await expect(rows).toHaveCount(3);
        await rows.last().getByLabel('Label for Port', { exact: true }).fill(' main ');
        await page.getByRole('button', { name: 'Save configuration', exact: true }).click();
        await expect(page.getByRole('alert')).toContainText('must have a non-empty, different label');
        expect(saved).toBeUndefined();
        await rows.last().getByLabel('Label for Port', { exact: true }).fill('');
        await page.getByRole('button', { name: 'Save configuration', exact: true }).click();
        await expect(page.getByRole('alert')).toContainText('must have a non-empty, different label');
        expect(saved).toBeUndefined();
        await rows.last().getByLabel('Label for Port', { exact: true }).fill('Extra B');
        await page.getByRole('button', { name: 'Save configuration', exact: true }).click();
        await expect.poll(() => parameters.map(parameter => [parameter.Label, parameter.Value])).toEqual([
            ['Backup', '6001'], ['Main', '5000'], ['Extra B', '7001'],
        ]);
        await section.getByRole('button', { name: 'Expand standalone parameters' }).click();
        await expect(rows).toHaveCount(3);
        await expect(rows.last().getByLabel('Label for Port', { exact: true })).toHaveValue('Main');
        await rows.last().getByRole('button', { name: 'Delete Port', exact: true }).click();
        await page.getByRole('button', { name: 'Save configuration', exact: true }).click();
        await expect.poll(() => saved?.DeleteConfigurationIds).toEqual(['main']);
        await section.getByRole('button', { name: 'Expand standalone parameters' }).click();
        await expect(rows).toHaveCount(2);
        await expect(rows.first().getByLabel('Label for Port', { exact: true })).toHaveValue('Backup');
        await expect(rows.last().getByLabel('Value for Port', { exact: true })).toHaveValue('7001');
    });
}
