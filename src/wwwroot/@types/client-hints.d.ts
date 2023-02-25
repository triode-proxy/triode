interface NavigatorUABrandVersion {
    brand: string;
    version: string;
}

interface UADataValues {
    brands: NavigatorUABrandVersion[];
    mobile: boolean;
    architecture: string;
    bitness: string;
    model: string;
    platform: string;
    platformVersion: string;
    /** @deprecated in favor of fullVersionList */
    uaFullVersion: string;
    wow64: boolean;
    fullVersionList: NavigatorUABrandVersion[];
}

interface NavigatorUAData {
    readonly brands: ReadonlyArray<NavigatorUABrandVersion>;
    readonly mobile: boolean;
    readonly platform: string;
    getHighEntropyValues(hints: Iterable<keyof UADataValues>): Promise<Partial<UADataValues>>;
}

interface Navigator {
    readonly userAgentData: NavigatorUAData;
}
