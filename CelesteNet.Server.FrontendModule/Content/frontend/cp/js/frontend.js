//@ts-check
import mdcrd from "./utils/mdcrd.js";
/** @type {import("material-components-web")} */
const mdc = window["mdc"];
import { FrontendSettings } from "./components/settings.js";
import { FrontendDOM } from "./components/dom.js";
import { FrontendUtils } from "./components/utils.js";
import { FrontendSync } from "./components/sync.js";
import { FrontendAuth } from "./components/auth.js";
import { FrontendDialog } from "./components/dialog.js";

export class Frontend {
  constructor() {
    /** @type {Map<string, HTMLElement>} */
    this.alerts = new Map();
    this.ready = false;
    this.renderable = false;

    this.gid = 0;

    this.MAX_INT = 4294967295;
  }

  async start() {
    // Init mdc early.
    mdc.autoInit();

    // Who would've known that the dialog component is needed early...
    this.dialog = new FrontendDialog(this);
    await this.dialog.start();

    // Set up all remaining components.
    this.utils = new FrontendUtils(this);
    this.settings = new FrontendSettings(this);
    this.sync = new FrontendSync(this);
    this.auth = new FrontendAuth(this);
    this.dom = new FrontendDOM(this);

    await this.dom.start();

    this.renderable = true;
    this.render();

    await this.auth.reauth();

    await this.sync.resync();

    // TODO: AAAAAAAAAAAAA
    for (let id of [
      "status",
      "accounts",
      "players",
      "channels",
      "cmd",
      "chat",
      "notes",
      "exec",
      "assemblies",
      "endpoints"
    ]) {
      const module = await import(`./panels/${id}.js`);
      for (let key of Reflect.ownKeys(module)) {
        let value = module[key];
        try {
          const p = new value(this);
          value.instance = p;
          p.id = p.id || id;
          await this.dom.add(p);
        } catch (e) {}
      }
    }

    document.getElementById("splash-progress-bar").style.transform = "scaleX(1)";

    setTimeout(() => {
      this.ready = true;
    }, 1200);
  }

  render() {
    if (!this.renderable)
      return;

    this.dom.render();
  }

  alert({
    title = "",
    text = "",
    defaultButton = "yes",
    buttons = ["OK"],
    dismissable = true
  }) {
    let key = [title, text, ...buttons].join("####");

    let el = this.alerts.get(key);
    el = mdcrd.dialog({ title, body: mdcrd.markdown(text), defaultButton, buttons })(el);
    this.alerts.set(key, el);
    document.body.appendChild(el);

    /** @type {import("@material/dialog").MDCDialog} */
    let dialog = el["MDCDialog"];

    if (!dismissable) {
      dialog.escapeKeyAction = "";
      dialog.scrimClickAction = "";
    } else {
      dialog.escapeKeyAction = "close";
      dialog.scrimClickAction = "close";
    }

    dialog.open();

    let promise = new Promise(resolve => el.addEventListener("MDCDialog:closed", e => resolve(e["detail"].action), { once: true }));
    dialog["then"] = promise.then.bind(promise);
    dialog["catch"] = promise.catch.bind(promise);

    return dialog;
  }

  snackbar({
    text = "",
    action = ""
  }) {
    // console.log("snackbar", text);

    if (this.snackbarLast) {
      // @ts-ignore Outdated .d.ts
      if (this.snackbarLast.isOpen && this.snackbarLastText === text)
        return;
      // @ts-ignore Outdated .d.ts
      this.snackbarLast.close("replaced");
    }

    let resolve;
    let promise = new Promise(_ => resolve = _);

    this.snackbarLastText = text;
    let el = mdcrd.snackbar(text, action, null)(null);
    document.body.appendChild(el);

    /** @type {import("@material/snackbar").MDCSnackbar & Promise<boolean>} */
    let snackbar = el["MDCSnackbar"];

    // @ts-ignore Outdated .d.ts
    snackbar.open();

    el.addEventListener("MDCSnackbar:closed", e => {
      resolve(e["detail"].reason === "action");
      setTimeout(() => {
        el.remove();
      }, 2000);
    }, { once: true });
    snackbar["then"] = promise.then.bind(promise);
    snackbar["catch"] = promise.catch.bind(promise);

    this.snackbarLast = snackbar;
    return snackbar;
  }

  /**
   * @param {string} value
   * @param {string} [censored]
   */
  censor(value, censored) {
    return this.settings.sensitive ? value : (censored || "CENSORED");
  }

}

const frontend = window["frontend"] = Frontend.instance = new Frontend();
