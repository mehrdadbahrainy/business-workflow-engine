import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePipe, JsonPipe } from '@angular/common';

type NodeType = 'start' | 'set' | 'gateway' | 'userTask' | 'http' | 'end';
type DataType = 'string' | 'number' | 'integer' | 'boolean';
interface SchemaField { name: string; type: DataType; required: boolean; }
interface Condition { path: string; operator: string; value: unknown; }
interface Transition { to: string; when: Condition | null; }
interface WorkflowNode { id: string; type: NodeType; config: Record<string, any>; transitions: Transition[]; }
interface WorkflowRole { id: string; name: string; }
interface WorkflowDefinition {
  schemaVersion: number; name: string; revision: number; title: string; description: string | null;
  inputSchema: Record<string, any>; outputSchema: Record<string, any>; roles: WorkflowRole[]; nodes: WorkflowNode[];
}
interface DefinitionSummary { name: string; revision: number; title: string; description: string | null; status: string; createdAt: string; publishedAt: string | null; }
interface WorkflowInstance { id: string; definitionName: string; definitionRevision: number; initiatorSubject: string; status: string; outcome: string | null; output: unknown; currentNodeId: string | null; createdAt: string; updatedAt: string; error: string | null; }
interface WorkItem { id: string; workflowInstanceId: string; assignedRole: string; title: string; input: unknown; completionSchema: Record<string, any> | null; createdAt: string; workflow: WorkflowInstance; }
interface IntegrationConnection { key: string; name: string; baseUrl: string; authHeaderName: string | null; authScheme: string | null; hasSecret: boolean; createdAt: string; updatedAt: string; }
interface InstanceDetails { instance: WorkflowInstance; input: unknown; output: unknown; steps: any[]; events: any[]; }

@Component({
  selector: 'app-root',
  imports: [FormsModule, DatePipe, JsonPipe],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App implements OnInit {
  private readonly http = inject(HttpClient);
  readonly actorStorageKey = 'demo-subject';
  actor = localStorage.getItem(this.actorStorageKey) ?? 'requester@example.test';
  rolesText = localStorage.getItem('demo-roles') ?? '';
  accessToken = sessionStorage.getItem('api-access-token') ?? '';
  activeTab: 'workflows' | 'designer' | 'integrations' | 'instances' | 'work' = 'workflows';
  definitions: DefinitionSummary[] = [];
  instances: WorkflowInstance[] = [];
  workItems: WorkItem[] = [];
  integrations: IntegrationConnection[] = [];
  integrationDraft = { key: '', name: '', baseUrl: '', authHeaderName: 'Authorization', authScheme: 'Bearer', secret: '', clearSecret: false };
  editingIntegration: string | null = null;
  selectedDefinition: DefinitionSummary | null = null;
  selectedInstance: InstanceDetails | null = null;
  definitionDraft = this.emptyDefinition();
  inputFields: SchemaField[] = [];
  outputFields: SchemaField[] = [];
  inputJson = '{\n  \n}';
  mappingTexts: Record<string, string> = {};
  conditionValueTexts: Record<string, string> = {};
  completionValues: Record<string, Record<string, unknown>> = {};
  editorMode: 'new' | 'edit' = 'new';
  busy = false;
  error = '';
  success = '';

  ngOnInit(): void { this.refreshAll(); }

  refreshAll(): void {
    this.error = '';
    this.http.get<DefinitionSummary[]>('/api/v1/workflow-definitions').subscribe({ next: value => this.definitions = value, error: e => this.showError(e) });
    this.refreshInstances();
    this.refreshWorkItems();
    this.refreshIntegrations();
  }

  refreshInstances(): void {
    this.http.get<WorkflowInstance[]>('/api/v1/workflow-instances').subscribe({ next: value => this.instances = value, error: e => this.showError(e) });
  }

  refreshWorkItems(): void {
    this.http.get<WorkItem[]>('/api/v1/work-items').subscribe({ next: value => {
      this.workItems = value;
      for (const item of value) this.completionValues[item.id] ??= this.completionExample(item.completionSchema);
    }, error: e => this.showError(e) });
  }

  switchActor(): void {
    localStorage.setItem(this.actorStorageKey, this.actor.trim());
    localStorage.setItem('demo-roles', this.rolesText.trim());
    location.reload();
  }

  saveAccessToken(): void {
    if (this.accessToken.trim()) sessionStorage.setItem('api-access-token', this.accessToken.trim());
    else sessionStorage.removeItem('api-access-token');
  }

  refreshIntegrations(): void { this.http.get<IntegrationConnection[]>('/api/v1/integrations').subscribe({ next: value => this.integrations = value, error: e => this.showError(e) }); }

  newIntegration(): void {
    this.integrationDraft = { key: '', name: '', baseUrl: '', authHeaderName: 'Authorization', authScheme: 'Bearer', secret: '', clearSecret: false };
    this.editingIntegration = null; this.error = ''; this.success = '';
  }

  editIntegration(connection: IntegrationConnection): void {
    this.integrationDraft = { key: connection.key, name: connection.name, baseUrl: connection.baseUrl, authHeaderName: connection.authHeaderName ?? '', authScheme: connection.authScheme ?? '', secret: '', clearSecret: false };
    this.editingIntegration = connection.key; this.error = ''; this.success = '';
  }

  saveIntegration(): void {
    this.busy = true; this.error = ''; this.success = '';
    const body = { ...this.integrationDraft, authHeaderName: this.integrationDraft.authHeaderName || null, authScheme: this.integrationDraft.authHeaderName === 'Authorization' ? this.integrationDraft.authScheme : null };
    const request = this.editingIntegration
      ? this.http.put(`/api/v1/integrations/${this.editingIntegration}`, body)
      : this.http.post('/api/v1/integrations', body);
    request.subscribe({ next: () => { this.busy = false; this.success = 'Integration connection saved. Secret values are never displayed again.'; this.newIntegration(); this.refreshIntegrations(); }, error: e => { this.busy = false; this.showError(e); } });
  }

  newWorkflow(): void {
    this.editorMode = 'new'; this.selectedDefinition = null; this.definitionDraft = this.emptyDefinition();
    this.inputFields = []; this.outputFields = []; this.mappingTexts = {}; this.activeTab = 'designer'; this.error = ''; this.success = '';
  }

  editWorkflow(summary: DefinitionSummary): void {
    this.http.get<WorkflowDefinition>(`/api/v1/workflow-definitions/${summary.name}/${summary.revision}`).subscribe({
      next: document => {
        this.editorMode = 'edit'; this.selectedDefinition = summary; this.definitionDraft = structuredClone(document);
        if (summary.status === 'published') this.definitionDraft.revision = summary.revision + 1;
        this.inputFields = this.readFields(document.inputSchema); this.outputFields = this.readFields(document.outputSchema);
        this.mappingTexts = {}; this.conditionValueTexts = {};
        this.activeTab = 'designer'; this.error = ''; this.success = '';
      }, error: e => this.showError(e)
    });
  }

  cloneWorkflow(summary: DefinitionSummary): void {
    this.http.get<WorkflowDefinition>(`/api/v1/workflow-definitions/${summary.name}/${summary.revision}`).subscribe({
      next: document => {
        this.editorMode = 'new'; this.selectedDefinition = null; this.definitionDraft = structuredClone(document);
        this.definitionDraft.name = `${document.name}-copy`; this.definitionDraft.title = `${document.title} copy`;
        this.definitionDraft.revision = 1; this.inputFields = this.readFields(document.inputSchema); this.outputFields = this.readFields(document.outputSchema);
        this.activeTab = 'designer'; this.success = 'A copy is ready to edit. Change its name before saving.';
      }, error: e => this.showError(e)
    });
  }

  addField(target: 'input' | 'output'): void {
    const fields = target === 'input' ? this.inputFields : this.outputFields;
    fields.push({ name: `field${fields.length + 1}`, type: 'string', required: false });
  }

  removeField(target: 'input' | 'output', index: number): void { (target === 'input' ? this.inputFields : this.outputFields).splice(index, 1); }

  addRole(): void { this.definitionDraft.roles.push({ id: `role-${this.definitionDraft.roles.length + 1}`, name: 'New role' }); }
  removeRole(index: number): void { this.definitionDraft.roles.splice(index, 1); }

  addNode(type: NodeType): void {
    if (type === 'start' && this.definitionDraft.nodes.some(n => n.type === 'start')) { this.error = 'A workflow has one start node.'; return; }
    const id = `${type.replace(/[A-Z]/g, x => `-${x.toLowerCase()}`)}-${this.definitionDraft.nodes.filter(n => n.type === type).length + 1}`;
    const node = this.createNode(id, type);
    const firstEnd = this.definitionDraft.nodes.find(n => n.type === 'end');
    if (type === 'end') this.definitionDraft.nodes.push(node);
    else if (firstEnd) {
      for (const current of this.definitionDraft.nodes) for (const transition of current.transitions) if (transition.to === firstEnd.id) transition.to = id;
      for (const transition of node.transitions) transition.to = firstEnd.id;
      this.definitionDraft.nodes.splice(this.definitionDraft.nodes.indexOf(firstEnd), 0, node);
    } else this.definitionDraft.nodes.push(node);
    this.error = '';
  }

  removeNode(node: WorkflowNode): void {
    if (node.type === 'start' || node.type === 'end') return;
    const destinations = node.transitions.map(x => x.to);
    for (const current of this.definitionDraft.nodes) for (const transition of current.transitions) if (transition.to === node.id) transition.to = destinations[0] ?? 'end';
    this.definitionDraft.nodes = this.definitionDraft.nodes.filter(x => x !== node);
  }

  renameNode(node: WorkflowNode, oldId: string): void {
    const id = node.id.trim().toLowerCase().replace(/[^a-z0-9-]/g, '-').replace(/-+/g, '-').replace(/^-|-$/g, '');
    node.id = id || oldId;
    for (const current of this.definitionDraft.nodes) for (const transition of current.transitions) if (transition.to === oldId) transition.to = node.id;
  }

  addTransition(node: WorkflowNode): void {
    node.transitions.push({ to: this.definitionDraft.nodes.find(x => x.type === 'end')?.id ?? '', when: node.type === 'gateway' ? { path: '$.input.value', operator: 'eq', value: true } : null });
  }

  setConditionMode(transition: Transition, mode: string): void {
    transition.when = mode === 'conditional' ? { path: '$.input.value', operator: 'eq', value: true } : null;
  }

  removeTransition(node: WorkflowNode, index: number): void { if (node.type === 'gateway' && node.transitions.length <= 2) return; node.transitions.splice(index, 1); }

  mappingText(node: WorkflowNode, property: string): string {
    const key = `${node.id}.${property}`;
    if (!(key in this.mappingTexts)) this.mappingTexts[key] = JSON.stringify(node.config[property] ?? {}, null, 2);
    return this.mappingTexts[key];
  }

  setMappingText(node: WorkflowNode, property: string, text: string): void {
    const key = `${node.id}.${property}`; this.mappingTexts[key] = text;
    try { node.config[property] = JSON.parse(text); delete node.config[`${property}Error`]; }
    catch { node.config[`${property}Error`] = 'Enter a valid JSON object.'; }
  }

  conditionValueText(node: WorkflowNode, index: number, transition: Transition): string {
    const key = `${node.id}.${index}`;
    return this.conditionValueTexts[key] ?? JSON.stringify(transition.when?.value ?? '');
  }

  setConditionValue(node: WorkflowNode, index: number, transition: Transition, text: string): void {
    this.conditionValueTexts[`${node.id}.${index}`] = text;
    if (!transition.when) transition.when = { path: '$.input.value', operator: 'eq', value: null };
    try { transition.when.value = JSON.parse(text); }
    catch { transition.when.value = text; }
  }

  saveDraft(): void {
    if (!this.syncSchemas()) return;
    this.busy = true; this.error = ''; this.success = '';
    const path = `/api/v1/workflow-definitions/${this.definitionDraft.name}/${this.definitionDraft.revision}`;
    this.http.put<DefinitionSummary>(path, this.definitionDraft).subscribe({
      next: () => { this.busy = false; this.success = 'Draft saved.'; this.refreshDefinitions(); },
      error: e => { this.busy = false; this.showError(e); }
    });
  }

  publishDraft(): void {
    if (!this.syncSchemas()) return;
    this.busy = true; this.error = ''; this.success = '';
    const base = `/api/v1/workflow-definitions/${this.definitionDraft.name}/${this.definitionDraft.revision}`;
    this.http.put(base, this.definitionDraft).subscribe({
      next: () => this.http.post<DefinitionSummary>(`${base}/publish`, {}).subscribe({
        next: () => { this.busy = false; this.success = 'Workflow published.'; this.refreshDefinitions(); this.activeTab = 'workflows'; },
        error: e => { this.busy = false; this.showError(e); }
      }), error: e => { this.busy = false; this.showError(e); }
    });
  }

  startWorkflow(summary: DefinitionSummary): void {
    if (this.selectedDefinition?.name !== summary.name) {
      this.selectedDefinition = summary; this.inputJson = this.exampleInput(summary.name);
    }
    this.error = ''; this.success = '';
  }

  submitWorkflow(): void {
    if (!this.selectedDefinition) return;
    let input: unknown;
    try { input = JSON.parse(this.inputJson); }
    catch { this.error = 'Workflow input must be valid JSON.'; return; }
    this.busy = true; this.error = ''; this.success = '';
    this.http.post<WorkflowInstance>(`/api/v1/workflows/${this.selectedDefinition.name}/instances`, input, { headers: { 'Idempotency-Key': crypto.randomUUID() } }).subscribe({
      next: result => { this.busy = false; this.success = `Workflow started: ${result.id}`; this.refreshInstances(); this.openInstance(result.id); },
      error: e => { this.busy = false; this.showError(e); }
    });
  }

  openInstance(id: string): void {
    this.http.get<InstanceDetails>(`/api/v1/workflow-instances/${id}`).subscribe({ next: value => this.selectedInstance = value, error: e => this.showError(e) });
  }

  completeWork(item: WorkItem): void {
    const form = this.completionValues[item.id] ?? {};
    const completion: Record<string, unknown> = {};
    for (const field of this.completionFields(item)) {
      const value = form[field.name];
      if (!field.required && (value === '' || value === null || value === undefined)) continue;
      completion[field.name] = value;
    }
    this.http.post(`/api/v1/work-items/${item.id}/complete`, completion).subscribe({
      next: () => { this.success = `${item.title} completed.`; this.refreshWorkItems(); this.refreshInstances(); this.openInstance(item.workflowInstanceId); },
      error: e => this.showError(e)
    });
  }

  transitionTargetNames(): string[] { return this.definitionDraft.nodes.map(x => x.id); }
  nodeLabel(type: string): string { return ({ start: 'Start', set: 'Set data', gateway: 'Conditional route', userTask: 'Role task', http: 'HTTP integration', end: 'End' } as Record<string, string>)[type] ?? type; }
  isPublished(summary: DefinitionSummary): boolean { return summary.status === 'published'; }

  private refreshDefinitions(): void { this.http.get<DefinitionSummary[]>('/api/v1/workflow-definitions').subscribe({ next: x => this.definitions = x, error: e => this.showError(e) }); }
  private syncSchemas(): boolean {
    if (!/^[a-z][a-z0-9-]{0,63}$/.test(this.definitionDraft.name)) { this.error = 'Workflow name must use lowercase letters, digits and hyphens.'; return false; }
    for (const [key, text] of Object.entries(this.mappingTexts)) {
      try { JSON.parse(text); }
      catch { this.error = `Fix the JSON mapping for ${key} before saving.`; return false; }
    }
    for (const node of this.definitionDraft.nodes) {
      if (node.config['valuesError'] || node.config['inputError'] || node.config['requestError'] || node.config['outputError']) { this.error = `Fix the JSON mapping in step ${node.id}.`; return false; }
    }
    this.definitionDraft.inputSchema = this.toSchema(this.inputFields);
    this.definitionDraft.outputSchema = this.toSchema(this.outputFields);
    return true;
  }
  private readFields(schema: Record<string, any>): SchemaField[] {
    const required = new Set<string>(schema['required'] ?? []);
    return Object.entries(schema['properties'] ?? {}).map(([name, value]: [string, any]) => ({ name, type: value.type, required: required.has(name) }));
  }
  private toSchema(fields: SchemaField[]): Record<string, any> {
    return { type: 'object', properties: Object.fromEntries(fields.filter(x => x.name.trim()).map(x => [x.name.trim(), { type: x.type }])), required: fields.filter(x => x.required && x.name.trim()).map(x => x.name.trim()), additionalProperties: false };
  }
  private emptyDefinition(): WorkflowDefinition {
    return { schemaVersion: 1, name: 'new-workflow', revision: 1, title: 'New workflow', description: '', inputSchema: this.toSchema([]), outputSchema: this.toSchema([]), roles: [], nodes: [this.createNode('start', 'start'), this.createNode('end', 'end')] };
  }
  private createNode(id: string, type: NodeType): WorkflowNode {
    const config: Record<string, any> = type === 'gateway' ? {} : type === 'set' ? { values: {} } : type === 'userTask' ? { role: this.definitionDraft?.roles?.[0]?.id ?? '', title: 'Review task', input: {}, completionSchema: { type: 'object', properties: { decision: { type: 'string' } }, required: ['decision'], additionalProperties: false } } : type === 'http' ? { method: 'POST', integrationKey: this.integrations[0]?.key ?? '', path: '/requests', request: {} } : type === 'end' ? { outcome: 'completed', output: {} } : {};
    const transitions: Transition[] = type === 'start' ? [{ to: 'end', when: null }] : type === 'gateway' ? [{ to: 'end', when: { path: '$.input.value', operator: 'eq', value: true } }, { to: 'end', when: null }] : type === 'end' ? [] : [{ to: 'end', when: null }];
    return { id, type, config, transitions };
  }
  private exampleInput(name: string): string {
    return name === 'purchase-approval' ? JSON.stringify({ requesterReference: 'REQ-1001', description: 'Replace office equipment', amount: 1800, currency: 'USD' }, null, 2)
      : name === 'employee-leave' ? JSON.stringify({ employeeId: 'EMP-1001', employeeName: 'Alex Example', days: 3, reason: 'Personal leave' }, null, 2) : '{\n  \n}';
  }
  private completionExample(schema: Record<string, any> | null): Record<string, unknown> {
    const example: Record<string, unknown> = {};
    const required = schema?.['required'] ?? [];
    const properties = schema?.['properties'] ?? {};
    for (const [name, definition] of Object.entries(properties) as [string, Record<string, any>][]) {
      const type = properties[name]?.type;
      if (definition['enum']?.length) example[name] = definition['enum'][0];
      else if (type === 'boolean') example[name] = required.includes(name) ? true : false;
      else if (type === 'number' || type === 'integer') example[name] = required.includes(name) ? 1 : null;
      else example[name] = '';
    }
    return example;
  }
  completionFields(item: WorkItem): { name: string; schema: Record<string, any>; required: boolean }[] {
    const properties = item.completionSchema?.['properties'] ?? {};
    const required: string[] = item.completionSchema?.['required'] ?? [];
    return Object.entries(properties).map(([name, schema]) => ({ name, schema: schema as Record<string, any>, required: required.includes(name) }));
  }
  private showError(error: HttpErrorResponse): void {
    const validation = error.error?.errors && Object.values(error.error.errors).flat().join(' ');
    this.error = error.status === 0 ? 'Could not reach the API. Start PostgreSQL and the API, then refresh.' : validation || error.error?.error || error.error?.title || `Request failed (${error.status}).`;
  }
}
